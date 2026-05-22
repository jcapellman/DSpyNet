// DSpyNet/DSPy.Teleprompters/GEPA.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public double ValidationSplit { get; set; } = 0.2;
        public int Seed { get; set; } = 0;
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
        private int _predictorCursor;

        public GEPA(ILM reflectionLM, FeedbackMetric metric, GEPAOptions options = null, ILogger logger = null)
        {
            _reflectionLM = reflectionLM ?? throw new ArgumentNullException(nameof(reflectionLM));
            _metric = metric ?? throw new ArgumentNullException(nameof(metric));
            _opts = options ?? new GEPAOptions();
            _logger = logger;
            _evaluator = new Evaluator(logger);
            _rng = new Random(_opts.Seed);
            _predictorCursor = 0;
        }

        public async Task<TModule> CompileAsync(TModule student, List<Example> trainset)
        {
            _logger?.LogInformation("Starting GEPA (Reflective Prompt Evolution)...");

            var (trainMini, valset) = Split(trainset, _opts.ValidationSplit);
            if (valset.Count == 0) valset = trainMini;

            var seed = (TModule)student.DeepClone();
            var seedScores = await _evaluator.EvaluatePerExampleAsync(seed, valset, _metric, _opts.FailureScore);
            var pool = new List<Candidate> { new Candidate(seed, seedScores) };

            int budget = _opts.MaxMetricCalls ?? AutoBudget(_opts.Auto, valset.Count);
            int metricUsed = valset.Count;
            _logger?.LogInformation($"Baseline avg score: {seedScores.Average():F3} | budget: {budget} metric calls");

            // Worst-case per iteration: trace collection (batch) + minibatch gate (2*batch) + valset eval.
            int worstCasePerIter = 3 * _opts.ReflectionMinibatchSize + valset.Count;
            while (metricUsed + worstCasePerIter <= budget)
            {
                var parent = SelectParent(pool);
                var named = parent.Program.NamedPredictorsWithNames();
                if (named.Count == 0) break;

                var (predName, targetPred) = named[_predictorCursor % named.Count];
                _predictorCursor++;

                var batch = SampleMinibatch(trainMini, _opts.ReflectionMinibatchSize);
                if (batch.Count == 0) continue;

                var (traces, feedbacks) = await CollectReflectionContextAsync(parent.Program, batch, predName, targetPred);
                metricUsed += batch.Count;

                var newInstruction = await ProposeInstructionAsync(targetPred.State.Instruction, predName, traces, feedbacks);
                if (string.IsNullOrWhiteSpace(newInstruction) || newInstruction == targetPred.State.Instruction)
                {
                    continue;
                }

                var child = (TModule)parent.Program.DeepClone();
                var childPred = child.NamedPredictorsWithNames().First(np => np.Name == predName).Predictor;
                childPred.State.Instruction = newInstruction;

                // Minibatch gate: keep child only if it doesn't regress on the batch it was tuned for.
                double parentBatchScore = await ScoreOnBatchAsync(parent.Program, batch);
                double childBatchScore = await ScoreOnBatchAsync(child, batch);
                metricUsed += 2 * batch.Count;

                if (childBatchScore + 1e-9 < parentBatchScore)
                {
                    _logger?.LogDebug($"[GEPA] Rejected child on minibatch ({childBatchScore:F3} < {parentBatchScore:F3})");
                    continue;
                }

                var childScores = await _evaluator.EvaluatePerExampleAsync(child, valset, _metric, _opts.FailureScore);
                metricUsed += valset.Count;

                pool.Add(new Candidate(child, childScores));
                _logger?.LogInformation($"[GEPA] +child via '{predName}' | avg {childScores.Average():F3} | pool {pool.Count} | used {metricUsed}/{budget}");
            }

            var best = pool.OrderByDescending(c => c.Aggregate).First();
            _logger?.LogInformation($"GEPA Finished. Best avg score: {best.Aggregate:F3} | candidates: {pool.Count}");
            return best.Program;
        }

        private async Task<string> ProposeInstructionAsync(string current, string predName, string traces, string feedback)
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
                }) as Prediction;

                var proposed = result?.Get<string>("ImprovedInstruction")?.Trim().Trim('"').Trim('`');
                return string.IsNullOrWhiteSpace(proposed) ? null : proposed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[GEPA] Reflection LM failed.");
                return null;
            }
        }

        private async Task<(string Traces, string Feedback)> CollectReflectionContextAsync(
            TModule program, List<Example> batch, string predName, IPredictor targetPred)
        {
            var tracesSb = new StringBuilder();
            var feedbackSb = new StringBuilder();
            var targetSigType = targetPred.State.Signature.SignatureType;

            // Sequential keeps AsyncLocal trace context coherent per example.
            for (int i = 0; i < batch.Count; i++)
            {
                var ex = batch[i];
                ExecutionState.BeginTrace();
                Prediction pred;
                try
                {
                    pred = (Prediction)await program.InvokeAsync(ex);
                }
                catch (Exception err)
                {
                    ExecutionState.EndTrace();
                    feedbackSb.AppendLine($"[Example {i + 1}] FAILED: {err.Message}");
                    continue;
                }
                var trace = ExecutionState.EndTrace() ?? new List<TraceEntry>();

                foreach (var t in trace.Where(t => t.SignatureState.Signature.SignatureType == targetSigType))
                {
                    tracesSb.AppendLine($"--- Example {i + 1} ---");
                    tracesSb.AppendLine("Input:");
                    foreach (var kv in t.Inputs.ToDictionary()) tracesSb.AppendLine($"  {kv.Key}: {kv.Value}");
                    tracesSb.AppendLine("Output:");
                    foreach (var kv in t.Outputs.ToDictionary()) tracesSb.AppendLine($"  {kv.Key}: {kv.Value}");
                }

                var fb = _metric(ex, pred, predName);
                feedbackSb.AppendLine($"[Example {i + 1}] score={fb.Score:F2} :: {fb.Feedback}");
            }

            return (tracesSb.ToString(), feedbackSb.ToString());
        }

        private async Task<double> ScoreOnBatchAsync(TModule program, List<Example> batch)
        {
            var scores = await _evaluator.EvaluatePerExampleAsync(program, batch, _metric, _opts.FailureScore);
            return scores.Length == 0 ? 0.0 : scores.Average();
        }

        // Weight by examples where this candidate ties for best; ties allowed.
        private Candidate SelectParent(List<Candidate> pool)
        {
            if (_opts.CandidateSelectionStrategy == GEPACandidateSelection.CurrentBest || pool.Count == 1)
            {
                return pool.OrderByDescending(c => c.Aggregate).First();
            }

            int n = pool[0].Scores.Length;
            var weights = new int[pool.Count];
            for (int i = 0; i < n; i++)
            {
                double best = pool.Max(c => c.Scores[i]);
                for (int j = 0; j < pool.Count; j++)
                {
                    if (pool[j].Scores[i] >= best - 1e-9) weights[j]++;
                }
            }

            int total = weights.Sum();
            if (total == 0) return pool[_rng.Next(pool.Count)];

            int draw = _rng.Next(total);
            int acc = 0;
            for (int j = 0; j < pool.Count; j++)
            {
                acc += weights[j];
                if (draw < acc) return pool[j];
            }
            return pool[^1];
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

        private static int AutoBudget(GEPAAutoBudget? auto, int valCount) => auto switch
        {
            GEPAAutoBudget.Light => 6 * valCount,
            GEPAAutoBudget.Medium => 12 * valCount,
            GEPAAutoBudget.Heavy => 18 * valCount,
            _ => 6 * valCount
        };

        private class Candidate
        {
            public TModule Program { get; }
            public double[] Scores { get; }
            public double Aggregate => Scores.Length == 0 ? 0.0 : Scores.Average();

            public Candidate(TModule program, double[] scores)
            {
                Program = program;
                Scores = scores;
            }
        }
    }
}
