// DSpyNet/DSPy.Teleprompters/MIPRO.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Evaluation;
using Microsoft.Extensions.Logging;

namespace DSpyNet.DSPy.Teleprompters
{
    public class MIPRO<TModule> : ITeleprompter<TModule> where TModule : Module
    {
        private readonly ILM _promptModel; // Model used to generate instructions
        private readonly Metric _metric;
        private readonly int _numCandidates; // How many instructions to generate
        private readonly int _numDemoSets;   // How many demo sets to generate
        private readonly int _numEvaluations; // How many random search iterations
        private readonly int _maxBootstrappedDemos;
        private readonly ILogger _logger;
        private readonly Evaluator _evaluator;

        public MIPRO(
            ILM promptModel,
            Metric metric,
            int numCandidates = 5,
            int numDemoSets = 3,
            int numEvaluations = 10,
            int maxBootstrappedDemos = 4,
            ILogger logger = null)
        {
            _promptModel = promptModel;
            _metric = metric;
            _numCandidates = numCandidates;
            _numDemoSets = numDemoSets;
            _numEvaluations = numEvaluations;
            _maxBootstrappedDemos = maxBootstrappedDemos;
            _logger = logger;
            _evaluator = new Evaluator(logger);
        }

        public async Task<TModule> CompileAsync(TModule student, List<Example> trainset)
        {
            _logger?.LogInformation("Starting MIPRO Optimization...");

            // 1. Generate Instruction Candidates
            var instructionCandidates = await GenerateInstructionsAsync(student, trainset);
            instructionCandidates.Insert(0, GetCurrentInstruction(student)); // Include original

            // 2. Generate Demo Set Candidates
            var demoSetCandidates = GenerateDemoSets(student, trainset);
            demoSetCandidates.Insert(0, new List<Example>()); // Include zero-shot (empty demos)

            _logger?.LogInformation($"Generated {instructionCandidates.Count} instructions and {demoSetCandidates.Count} demo sets.");

            // 3. Random Search Loop
            TModule bestModule = (TModule)student.DeepClone();
            double bestScore = double.MinValue;

            var random = new Random();

            // We split trainset into train and val for evaluation during search if not provided explicitly.
            // For simplicity here, we use the FULL trainset for evaluation (assuming small dataset in tests),
            // but in prod we should split.
            var evalSet = trainset; 

            for (int i = 0; i < _numEvaluations; i++)
            {
                // Pick random pair
                var instr = instructionCandidates[random.Next(instructionCandidates.Count)];
                var demos = demoSetCandidates[random.Next(demoSetCandidates.Count)];

                _logger?.LogInformation($"[MIPRO] Iteration {i+1}/{_numEvaluations}. Eval...");

                // Config student
                var candidate = (TModule)student.DeepClone();
                ApplyConfig(candidate, instr, demos);

                // Eval
                double score = await _evaluator.EvaluateAsync(candidate, evalSet, _metric);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestModule = candidate;
                    _logger?.LogInformation($"[MIPRO] New Best Score: {bestScore}%");
                }
            }

            _logger?.LogInformation($"MIPRO Finished. Best Score: {bestScore}%");
            return bestModule;
        }

        private async Task<List<string>> GenerateInstructionsAsync(TModule student, List<Example> trainset)
        {
            var candidates = new List<string>();
            
            // Create a generator module
            var generator = new Predict<GenerateInstructionSignature>(_promptModel);
            
            // Format a few examples for the meta-prompt (take 3 random)
            var summaryExamples = trainset.Take(3).Select(ex => ex.ToString());
            var examplesStr = string.Join("\n", summaryExamples);
            var basicInstr = GetCurrentInstruction(student);

            for (int i = 0; i < _numCandidates; i++)
            {
                try 
                {
                    // High temperature is usually preferred here, but ILM interface hides settings.
                    // Assuming ILM is configured or we rely on default variability.
                    var result = await generator.InvokeAsync(new { 
                        BasicInstruction = basicInstr,
                        Examples = examplesStr
                    }) as Prediction;

                    if (result != null)
                    {
                        var proposed = result.Get<string>("ProposedInstruction");
                        if (!string.IsNullOrWhiteSpace(proposed))
                        {
                            candidates.Add(proposed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error generating instruction candidate");
                }
            }

            return candidates;
        }

        private List<List<Example>> GenerateDemoSets(TModule student, List<Example> trainset)
        {
            var sets = new List<List<Example>>();
            var random = new Random();

            // Simple strategy: Random sampling from trainset.
            // In a full version, we would use BootstrapFewShot logic here to generate *traces*.
            // For now, we assume provided examples are good enough (Labeled Few Shot behavior).
            
            for (int i = 0; i < _numDemoSets; i++)
            {
                var subset = trainset.OrderBy(x => random.Next()).Take(_maxBootstrappedDemos).ToList();
                sets.Add(subset);
            }

            return sets;
        }

        // Reflection helper to get/set state on the module
        private string GetCurrentInstruction(TModule module)
        {
            // Assuming module is Predict<T> or ChainOfThought<T> which has State property
            var prop = module.GetType().GetProperty("State");
            if (prop != null)
            {
                var state = prop.GetValue(module) as SignatureState;
                return state?.Instruction ?? "";
            }
            return "";
        }

        private void ApplyConfig(TModule module, string instruction, List<Example> demos)
        {
            var prop = module.GetType().GetProperty("State");
            if (prop != null)
            {
                var state = prop.GetValue(module) as SignatureState;
                if (state != null)
                {
                    state.Instruction = instruction;
                    state.Demos = new List<Example>(demos);
                }
            }
        }
    }
}