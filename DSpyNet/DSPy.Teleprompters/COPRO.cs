// DSpyNet/DSPy.Teleprompters/COPRO.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Evaluation;
using DSpyNet.DSPy.Execution;

namespace DSpyNet.DSPy.Teleprompters
{
    public class COPRO<TModule> : ITeleprompter<TModule> where TModule : Module
    {
        private readonly ILM _promptModel;
        private readonly Metric _metric;
        private readonly int _breadth; // How many candidates to generate per step
        private readonly int _depth;   // How many optimization rounds
        private readonly ILogger _logger;
        private readonly Evaluator _evaluator;

        // Track history of candidates: PredictorID -> List of (Instruction, Score, Depth)
        private readonly Dictionary<string, List<CandidateHistory>> _history = new();

        public COPRO(
            ILM promptModel,
            Metric metric,
            int breadth = 5,
            int depth = 3,
            ILogger logger = null)
        {
            _promptModel = promptModel;
            _metric = metric;
            _breadth = breadth;
            _depth = depth;
            _logger = logger;
            _evaluator = new Evaluator(logger);
        }

        private class CandidateHistory
        {
            public string Instruction { get; set; }
            public string Prefix { get; set; }
            public double Score { get; set; }
            public int Depth { get; set; }
        }

        public async Task<TModule> CompileAsync(TModule student, List<Example> trainset)
        {
            _logger?.LogInformation("Starting COPRO Optimization...");

            // Evaluate Baseline
            double bestScore = await _evaluator.EvaluateAsync(student, trainset, _metric);
            _logger?.LogInformation($"Baseline Score: {bestScore}%");

            TModule bestProgram = (TModule)student.DeepClone();

            // Initialize Predictors mapping
            // Note: Since Module.DeepClone creates new objects, object references change.
            // We need a stable way to identify predictors. We will use their index in the traversal list.
            // Assumption: The structure of the module graph is static.

            for (int d = 0; d < _depth; d++)
            {
                _logger?.LogInformation($"--- COPRO Depth {d + 1}/{_depth} ---");
                
                // We optimize each predictor in turn (Coordinate Ascent)
                var currentPredictors = bestProgram.NamedPredictors();
                
                for (int predIdx = 0; predIdx < currentPredictors.Count; predIdx++)
                {
                    _logger?.LogInformation($"Optimizing Predictor #{predIdx}...");
                    
                    var predictorToOptimize = currentPredictors[predIdx];
                    var predictorKey = $"Pred_{predIdx}"; // Key to track history

                    if (!_history.ContainsKey(predictorKey)) _history[predictorKey] = new List<CandidateHistory>();

                    // 1. Generate Candidates
                    var candidates = await GenerateCandidatesAsync(predictorToOptimize, predictorKey);

                    // 2. Evaluate Candidates
                    foreach (var cand in candidates)
                    {
                        // Create a temporary program with this candidate instruction applied
                        var candidateProgram = (TModule)bestProgram.DeepClone();
                        var candPredictors = candidateProgram.NamedPredictors();
                        var targetPredictor = candPredictors[predIdx];

                        // Apply Candidate
                        targetPredictor.State.Instruction = cand.Instruction;
                        
                        // Apply Prefix to the LAST output field (standard DSPy behavior)
                        var lastOutput = targetPredictor.State.Signature.OutputFields.LastOrDefault();
                        if (lastOutput != null && !string.IsNullOrEmpty(cand.Prefix))
                        {
                            lastOutput.Prefix = cand.Prefix;
                        }

                        // Eval
                        double score = await _evaluator.EvaluateAsync(candidateProgram, trainset, _metric);
                        _logger?.LogInformation($"  Cand Score: {score:F2}% | Instr: {cand.Instruction.Substring(0, Math.Min(30, cand.Instruction.Length))}...");

                        // Update History
                        cand.Score = score;
                        cand.Depth = d;
                        _history[predictorKey].Add(cand);

                        // Update Global Best if found
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestProgram = candidateProgram; // Snapshot this new best state
                            _logger?.LogInformation($"  🚀 New Best Score: {bestScore}%");
                        }
                    }
                    
                    // After iterating candidates for this predictor, we stick with the bestProgram found SO FAR 
                    // before moving to the next predictor in the same depth cycle. 
                    // This is the essence of Coordinate Ascent.
                }
            }

            _logger?.LogInformation($"COPRO Finished. Final Score: {bestScore}%");
            return bestProgram;
        }

        private async Task<List<CandidateHistory>> GenerateCandidatesAsync(IPredictor predictor, string predictorKey)
        {
            var candidates = new List<CandidateHistory>();
            var history = _history[predictorKey];
            
            // Generate meta-prompt
            // We use a Predict module to call the Prompt Model
            
            // If history is empty, do Zero-Shot generation
            if (history.Count == 0)
            {
                var generator = new Predict<BasicGenerateInstruction>(_promptModel);
                // We ask for multiple candidates. In standard DSPy this is done via 'n' param in config.
                // Here our ILM is simple, so we just loop.
                
                for(int i=0; i < _breadth; i++)
                {
                    try 
                    {
                        var result = await generator.InvokeAsync(new { 
                            BasicInstruction = predictor.State.Signature.Instruction 
                        }) as Prediction;
                        
                        if (result != null)
                        {
                            candidates.Add(new CandidateHistory
                            {
                                Instruction = result.Get<string>("ProposedInstruction"),
                                Prefix = result.Get<string>("ProposedPrefixForOutputField")
                            });
                        }
                    }
                    catch { /* ignore gen errors */ }
                }
            }
            else
            {
                // Few-Shot generation based on history
                // Sort history by score ascending (best last)
                var sortedHistory = history.OrderBy(x => x.Score).ToList();
                
                var sb = new StringBuilder();
                foreach(var h in sortedHistory)
                {
                    sb.AppendLine($"Instruction: {h.Instruction}");
                    if(!string.IsNullOrEmpty(h.Prefix)) sb.AppendLine($"Prefix: {h.Prefix}");
                    sb.AppendLine($"Score: {h.Score}");
                    sb.AppendLine("---");
                }

                var generator = new Predict<GenerateInstructionGivenAttempts>(_promptModel);
                
                for(int i=0; i < _breadth; i++)
                {
                    try 
                    {
                        var result = await generator.InvokeAsync(new { 
                            AttemptedInstructions = sb.ToString()
                        }) as Prediction;
                        
                        if (result != null)
                        {
                            candidates.Add(new CandidateHistory
                            {
                                Instruction = result.Get<string>("ProposedInstruction"),
                                Prefix = result.Get<string>("ProposedPrefixForOutputField")
                            });
                        }
                    }
                    catch { /* ignore */ }
                }
            }
            
            // Filter out empty instructions
            return candidates.Where(c => !string.IsNullOrWhiteSpace(c.Instruction)).ToList();
        }
    }
}