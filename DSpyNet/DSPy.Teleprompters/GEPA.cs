// DSpyNet/DSPy.Teleprompters/GEPA.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Evaluation;
using DSpyNet.DSPy.Execution;
using DSpyNet.DSPy.Modules;

namespace DSpyNet.DSPy.Teleprompters
{
    public enum GEPAAutoBudget { Light, Medium, Heavy }

    public enum GEPACandidateSelection { Pareto, CurrentBest }

    public class GEPAOptions
    {
        public GEPAAutoBudget? Auto { get; set; } = GEPAAutoBudget.Light;
        public int? MaxMetricCalls { get; set; }
        public int ReflectionMinibatchSize { get; set; } = 3;
        public GEPACandidateSelection CandidateSelectionStrategy { get; set; } = GEPACandidateSelection.Pareto;
        public double FailureScore { get; set; } = 0.0;
        public double PerfectScore { get; set; } = 1.0; // skip reflection if minibatch hits this
        public double ValidationSplit { get; set; } = 0.2;
        public int Seed { get; set; } = 0;
        public int MergeEvery { get; set; } = 5; // 0 disables
        public bool UseMerge { get; set; } = true;
    }

    /// <summary>
    /// Reflective prompt-evolution optimizer (Genetic-Pareto).
    /// Mutates one predictor's instruction per iteration via the reflection LM; keeps a Pareto pool.
    /// </summary>
    public class GEPA<TModule> : ITeleprompter<TModule> where TModule : Module
    {
        private readonly ILM _reflectionLM;
        private readonly FeedbackMetric _metric;
        private readonly GEPAOptions _opts;
        private readonly ILogger _logger;
        private readonly Evaluator _evaluator;
        private readonly Random _rng;

        public GEPA(ILM reflectionLM, FeedbackMetric metric, GEPAOptions options = null, ILogger logger = null)
        {
            _reflectionLM = reflectionLM ?? throw new ArgumentNullException(nameof(reflectionLM));
            _metric = metric ?? throw new ArgumentNullException(nameof(metric));
            _opts = options ?? new GEPAOptions();
            _logger = logger;
            _evaluator = new Evaluator(logger);
            _rng = new Random(_opts.Seed);
        }

        public async Task<TModule> CompileAsync(TModule student, List<Example> trainset, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Starting GEPA (Reflective Prompt Evolution)...");

            var (trainMini, valset) = Split(trainset, _opts.ValidationSplit);
            if (valset.Count == 0) valset = trainMini;

            var seed = (TModule)student.DeepClone();
            var seedNamed = seed.NamedPredictorsWithNames();
            if (seedNamed.Count == 0)
            {
                _logger?.LogWarning("[GEPA] No predictors discovered on student; returning seed unchanged.");
                return seed;
            }

            var seedScores = await _evaluator.EvaluatePerExampleAsync(seed, valset, _metric, _opts.FailureScore, cancellationToken: cancellationToken);
            var pool = new List<Candidate> { new Candidate(seed, seedScores, predictorCursor: 0) };

            int budget = _opts.MaxMetricCalls ?? AutoBudget(_opts.Auto, valset.Count, seedNamed.Count);
            int metricUsed = valset.Count;
            _logger?.LogInformation($"Baseline avg score: {seedScores.Average():F3} | budget: {budget} metric calls");

            int iter = 0;
            int consecutiveNoOpReflective = 0;
            while (metricUsed < budget)
            {
                cancellationToken.ThrowIfCancellationRequested();

                iter++;
                bool tryMerge = _opts.UseMerge && _opts.MergeEvery > 0 && iter % _opts.MergeEvery == 0;

                StepResult step = tryMerge
                    ? await TryMergeAsync(pool, valset, budget - metricUsed, cancellationToken)
                    : await TryReflectiveStepAsync(pool, trainMini, valset, budget - metricUsed, cancellationToken);

                metricUsed += step.Spent;
                if (step.Accepted != null)
                {
                    pool.Add(step.Accepted);
                    _logger?.LogInformation($"[GEPA] +candidate ({(tryMerge ? "merge" : "reflect")}) | avg {step.Accepted.Aggregate:F3} | pool {pool.Count} | used {metricUsed}/{budget}");
                }

                // Bail if reflective steps stop spending; merge no-ops are not a stall signal.
                if (!tryMerge && step.Spent == 0) consecutiveNoOpReflective++;
                else if (!tryMerge) consecutiveNoOpReflective = 0;
                if (consecutiveNoOpReflective >= 3) break;
            }

            var best = pool.OrderByDescending(c => c.Aggregate).First();
            _logger?.LogInformation($"GEPA Finished. Best avg score: {best.Aggregate:F3} | candidates: {pool.Count}");
            return best.Program;
        }

        // Reflective step: pick parent + predictor, collect traces, propose, gate on minibatch, eval on valset.
        private async Task<StepResult> TryReflectiveStepAsync(
            List<Candidate> pool, List<Example> trainMini, List<Example> valset, int budgetRemaining, CancellationToken cancellationToken)
        {
            var parent = SelectParent(pool);
            var named = parent.Program.NamedPredictorsWithNames();
            if (named.Count == 0) return new StepResult(0, null);

            var (predName, targetPred) = named[parent.PredictorCursor % named.Count];

            var batch = SampleMinibatch(trainMini, _opts.ReflectionMinibatchSize);
            if (batch.Count == 0 || batch.Count > budgetRemaining) return new StepResult(0, null);

            var (traces, feedback, batchScores, batchSucceeded) =
                await CollectReflectionContextAsync(parent.Program, batch, predName, targetPred, cancellationToken);
            int spent = batch.Count;

            if (batchSucceeded && batchScores.All(s => s + 1e-9 >= _opts.PerfectScore))
            {
                _logger?.LogDebug($"[GEPA] Minibatch all perfect; skipping reflection on '{predName}'.");
                return new StepResult(spent, null);
            }

            var newInstruction = await ProposeInstructionAsync(targetPred.State.Instruction, predName, traces, feedback, cancellationToken);
            if (string.IsNullOrWhiteSpace(newInstruction) || newInstruction == targetPred.State.Instruction)
            {
                return new StepResult(spent, null);
            }

            var child = (TModule)parent.Program.DeepClone();
            var childPredEntry = child.NamedPredictorsWithNames().FirstOrDefault(np => np.Name == predName);
            if (childPredEntry.Predictor == null) return new StepResult(spent, null);
            childPredEntry.Predictor.State.Instruction = newInstruction;

            // Strict-improvement gate: reject children that don't beat the parent on their own training batch.
            if (spent + batch.Count > budgetRemaining) return new StepResult(spent, null);
            double parentBatchScore = batchScores.Average();
            double childBatchScore = await ScoreOnBatchAsync(child, batch, cancellationToken);
            spent += batch.Count;

            if (childBatchScore <= parentBatchScore + 1e-9)
            {
                _logger?.LogDebug($"[GEPA] Rejected child on minibatch ({childBatchScore:F3} <= {parentBatchScore:F3})");
                return new StepResult(spent, null);
            }

            if (spent + valset.Count > budgetRemaining)
            {
                _logger?.LogDebug($"[GEPA] Skipping valset eval; would exceed budget.");
                return new StepResult(spent, null);
            }

            var childScores = await _evaluator.EvaluatePerExampleAsync(child, valset, _metric, _opts.FailureScore, cancellationToken: cancellationToken);
            spent += valset.Count;

            return new StepResult(spent, new Candidate(child, childScores, parent.PredictorCursor + 1));
        }

        // Merge proposer: pick two non-dominated parents and mix their predictor instructions component-wise.
        private async Task<StepResult> TryMergeAsync(List<Candidate> pool, List<Example> valset, int budgetRemaining, CancellationToken cancellationToken)
        {
            var front = NonDominated(pool);
            if (front.Count < 2 || valset.Count > budgetRemaining) return new StepResult(0, null);

            var a = front[_rng.Next(front.Count)];
            Candidate b;
            do { b = front[_rng.Next(front.Count)]; } while (ReferenceEquals(a, b));

            var child = (TModule)a.Program.DeepClone();
            var bByName = b.Program.NamedPredictorsWithNames().ToDictionary(x => x.Name, x => x.Predictor);

            bool changedAny = false;
            foreach (var (name, pred) in child.NamedPredictorsWithNames())
            {
                if (!bByName.TryGetValue(name, out var bPred)) continue;
                if (_rng.Next(2) == 1 && bPred.State.Instruction != pred.State.Instruction)
                {
                    pred.State.Instruction = bPred.State.Instruction;
                    changedAny = true;
                }
            }
            if (!changedAny) return new StepResult(0, null);

            var childScores = await _evaluator.EvaluatePerExampleAsync(child, valset, _metric, _opts.FailureScore, cancellationToken: cancellationToken);
            int cursor = Math.Max(a.PredictorCursor, b.PredictorCursor);
            return new StepResult(valset.Count, new Candidate(child, childScores, cursor));
        }

        private readonly struct StepResult
        {
            public int Spent { get; }
            public Candidate Accepted { get; }
            public StepResult(int spent, Candidate accepted) { Spent = spent; Accepted = accepted; }
        }

        private async Task<string> ProposeInstructionAsync(string current, string predName, string traces, string feedback, CancellationToken cancellationToken)
        {
            try
            {
                var proposer = new Predict<GEPAReflectionSignature>(_reflectionLM);
                var result = await proposer.InvokeAsync(new
                {
                    CurrentInstruction = current,
                    PredictorName = predName,
                    Traces = traces,
                    Feedback = feedback
                }, cancellationToken) as Prediction;

                var proposed = ExtractInstruction(result?.Get<string>("ImprovedInstruction"));
                return string.IsNullOrWhiteSpace(proposed) ? null : proposed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[GEPA] Reflection LM failed.");
                return null;
            }
        }

        // Strip ``` fences (with optional language spec) since the reflection prompt asks for them.
        private static string ExtractInstruction(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();

            int firstFence = s.IndexOf("```", StringComparison.Ordinal);
            int lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (firstFence >= 0 && lastFence > firstFence)
            {
                int contentStart = firstFence + 3;
                int newline = s.IndexOf('\n', contentStart);
                if (newline > 0 && newline < lastFence)
                {
                    string firstLine = s.Substring(contentStart, newline - contentStart).Trim();
                    if (!firstLine.Contains(' ') && firstLine.Length < 20) contentStart = newline + 1; // language spec
                }
                s = s.Substring(contentStart, lastFence - contentStart).Trim();
            }
            else if (firstFence >= 0)
            {
                int newline = s.IndexOf('\n', firstFence);
                if (newline > 0) s = s.Substring(newline + 1).Trim();
            }

            return s.Trim().Trim('"').Trim();
        }

        // Per-example trace + feedback for the target predictor, attributed by identity (not signature type).
        private async Task<(string Traces, string Feedback, double[] Scores, bool AllSucceeded)>
            CollectReflectionContextAsync(TModule program, List<Example> batch, string predName, IPredictor targetPred, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            var scores = new double[batch.Count];
            bool allSucceeded = true;

            // Sequential keeps AsyncLocal trace context coherent per example.
            for (int i = 0; i < batch.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ex = batch[i];
                ExecutionState.BeginTrace();
                Prediction pred = null;
                Exception runError = null;
                try { pred = (Prediction)await program.InvokeAsync(ex, cancellationToken); }
                catch (Exception err) { runError = err; }
                var trace = ExecutionState.EndTrace() ?? new List<TraceEntry>();

                sb.AppendLine($"# Example {i + 1}");

                if (runError != null)
                {
                    sb.AppendLine("## Run failed");
                    sb.AppendLine("```"); sb.AppendLine(runError.Message); sb.AppendLine("```");
                    scores[i] = _opts.FailureScore;
                    allSucceeded = false;
                    continue;
                }

                var relevant = trace.Where(t => ReferenceEquals(t.Predictor, targetPred)).ToList();
                if (relevant.Count == 0)
                {
                    sb.AppendLine("## (no traces for target predictor)");
                }
                else
                {
                    foreach (var t in relevant)
                    {
                        sb.AppendLine("## Inputs");
                        foreach (var kv in t.Inputs.ToDictionary()) sb.AppendLine($"- {kv.Key}: {kv.Value}");
                        sb.AppendLine("## Generated Outputs");

                        var expected = t.SignatureState?.Signature?.OutputFields;
                        var produced = t.Outputs?.ToDictionary() ?? new Dictionary<string, object>();
                        bool parseFailed = expected != null &&
                            expected.Count > 0 &&
                            expected.All(f => !produced.ContainsKey(f.Name) || string.IsNullOrEmpty(produced[f.Name]?.ToString()));

                        if (parseFailed)
                        {
                            sb.AppendLine("(failed to parse — raw response below)");
                            sb.AppendLine("```"); sb.AppendLine(t.RawResponse ?? ""); sb.AppendLine("```");
                            sb.Append("Expected fields: ");
                            sb.AppendLine(string.Join(", ", expected.Select(f => f.Name)));
                        }
                        else
                        {
                            foreach (var kv in produced) sb.AppendLine($"- {kv.Key}: {kv.Value}");
                        }
                    }
                }

                var fb = _metric(ex, pred, predName);
                scores[i] = fb.Score;
                sb.AppendLine("## Feedback");
                sb.AppendLine($"score={fb.Score:F2} :: {fb.Feedback}");
                sb.AppendLine();
            }

            return (sb.ToString(), $"Per-example scores: {string.Join(", ", scores.Select(s => s.ToString("F2")))}", scores, allSucceeded);
        }

        private async Task<double> ScoreOnBatchAsync(TModule program, List<Example> batch, CancellationToken cancellationToken)
        {
            var scores = await _evaluator.EvaluatePerExampleAsync(program, batch, _metric, _opts.FailureScore, cancellationToken: cancellationToken);
            return scores.Length == 0 ? 0.0 : scores.Average();
        }

        // Weight by examples where this candidate ties for best; ties allowed.
        private Candidate SelectParent(List<Candidate> pool)
        {
            if (_opts.CandidateSelectionStrategy == GEPACandidateSelection.CurrentBest || pool.Count == 1)
            {
                return pool.OrderByDescending(c => c.Aggregate).First();
            }

            var front = NonDominated(pool);
            if (front.Count == 1) return front[0];

            int n = front[0].Scores.Length;
            var weights = new int[front.Count];
            for (int i = 0; i < n; i++)
            {
                double best = front.Max(c => c.Scores[i]);
                for (int j = 0; j < front.Count; j++)
                {
                    if (front[j].Scores[i] >= best - 1e-9) weights[j]++;
                }
            }

            int total = weights.Sum();
            if (total == 0) return front[_rng.Next(front.Count)];

            int draw = _rng.Next(total);
            int acc = 0;
            for (int j = 0; j < front.Count; j++)
            {
                acc += weights[j];
                if (draw < acc) return front[j];
            }
            return front[^1];
        }

        // A dominates B iff A >= B on every example and > on at least one.
        private static List<Candidate> NonDominated(List<Candidate> pool)
        {
            var front = new List<Candidate>();
            for (int i = 0; i < pool.Count; i++)
            {
                bool dominated = false;
                for (int j = 0; j < pool.Count; j++)
                {
                    if (i == j) continue;
                    if (Dominates(pool[j], pool[i])) { dominated = true; break; }
                }
                if (!dominated) front.Add(pool[i]);
            }
            return front;
        }

        private static bool Dominates(Candidate a, Candidate b)
        {
            if (a.Scores.Length != b.Scores.Length) return false;
            bool strict = false;
            for (int i = 0; i < a.Scores.Length; i++)
            {
                if (a.Scores[i] + 1e-9 < b.Scores[i]) return false;
                if (a.Scores[i] > b.Scores[i] + 1e-9) strict = true;
            }
            return strict;
        }

        private List<Example> SampleMinibatch(List<Example> trainMini, int size)
        {
            if (trainMini.Count <= size) return new List<Example>(trainMini);
            return trainMini.OrderBy(_ => _rng.Next()).Take(size).ToList();
        }

        private (List<Example> mini, List<Example> val) Split(List<Example> trainset, double valFrac)
        {
            if (trainset.Count <= 2) return (trainset, trainset);
            int valCount = Math.Max(1, (int)Math.Round(trainset.Count * valFrac));
            int miniCount = trainset.Count - valCount;
            return (trainset.Take(miniCount).ToList(), trainset.Skip(miniCount).ToList());
        }

        // From DSPy: V + 5C + N*M + (V/full_eval_steps + 1)*V where N = max(4*num_preds*log2(C), 1.5*C).
        private int AutoBudget(GEPAAutoBudget? auto, int valCount, int numPreds)
        {
            int numCandidates = auto switch
            {
                GEPAAutoBudget.Light => 6,
                GEPAAutoBudget.Medium => 12,
                GEPAAutoBudget.Heavy => 18,
                _ => 6
            };
            int m = Math.Max(1, _opts.ReflectionMinibatchSize);
            int v = Math.Max(1, valCount);
            double logC = Math.Log2(Math.Max(2, numCandidates));
            int n = (int)Math.Ceiling(Math.Max(2.0 * numPreds * 2.0 * logC, 1.5 * numCandidates));
            int fullEvalSteps = 5;
            return v + 5 * numCandidates + n * m + (v / fullEvalSteps + 1) * v;
        }

        private class Candidate
        {
            public TModule Program { get; }
            public double[] Scores { get; }
            public double Aggregate => Scores == null || Scores.Length == 0 ? 0.0 : Scores.Average();
            public int PredictorCursor { get; } // child = parent + 1 so siblings rotate independently

            public Candidate(TModule program, double[] scores, int predictorCursor)
            {
                Program = program;
                Scores = scores;
                PredictorCursor = predictorCursor;
            }
        }
    }
}
