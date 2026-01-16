// DSPy.Teleprompters/BootstrapFewShot.cs

using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;
using DSpyNet.DSPy.Modules;
using Microsoft.Extensions.Logging;

namespace DSpyNet.DSPy.Teleprompters
{
    /// <summary>
    /// A simple teleprompter that optimizes signatures by bootstrapping few-shot examples.
    /// It runs a teacher module over the training set, collects traces, checks them against a metric,
    /// and adds successful traces as demos to the student.
    /// </summary>
    public class BootstrapFewShot<TModule> : ITeleprompter<TModule> where TModule : Module
    {
        private readonly Metric _metric;
        private readonly int _maxLabeledDemos;
        private readonly int _maxBootstrappedDemos;
        private readonly TModule _teacher;
        private readonly ILogger _logger;

        public BootstrapFewShot(
            Metric metric, 
            TModule teacher = null, 
            int maxBootstrappedDemos = 4, 
            int maxLabeledDemos = 4,
            ILogger logger = null)
        {
            _metric = metric;
            _teacher = teacher;
            _maxBootstrappedDemos = maxBootstrappedDemos;
            _maxLabeledDemos = maxLabeledDemos;
            _logger = logger;
        }

        public async Task<TModule> CompileAsync(TModule student, List<Example> trainset)
        {
            _logger?.LogInformation("Starting BootstrapFewShot compilation...");

            // 1. Prepare Student and Teacher
            var studentClone = (TModule)student.DeepClone();
            var teacher = _teacher ?? (TModule)student.DeepClone(); // Use student as teacher if none provided

            var name2traces = new Dictionary<string, List<Example>>();

            // 2. Run Teacher on Trainset to collect traces
            foreach (var example in trainset)
            {
                // We stop if we have enough demos for all predictors (simplified logic: check global count)
                // In a real robust implementation, we check per-predictor counts.
                
                try
                {
                    // Enable Tracing!
                    ExecutionState.BeginTrace();

                    var predictionObj = await teacher.InvokeAsync(example);
                    
                    Prediction prediction;
                    if (predictionObj is Prediction p) prediction = p;
                    else continue;

                    // 3. Validate Result
                    if (_metric(example, prediction))
                    {
                        // Success! Let's grab the trace.
                        var trace = ExecutionState.EndTrace();
                        
                        foreach (var step in trace)
                        {
                            // step contains: SignatureState, Inputs, Outputs
                            // We need to create a "Demo" example that combines Input and Output.
                            var demoData = new Dictionary<string, object>();
                            
                            // Copy inputs
                            foreach (var kvp in step.Inputs.ToDictionary())
                                demoData[kvp.Key] = kvp.Value;
                                
                            // Copy outputs
                            foreach (var kvp in step.Outputs.ToDictionary())
                                demoData[kvp.Key] = kvp.Value;

                            var demo = new Example(demoData);

                            // We need to associate this demo with a specific predictor signature.
                            // Since we don't have unique IDs for modules yet, we use the Signature type name or Instruction hash.
                            // Simple approach: use the Instruction text as key for matching.
                            var key = step.SignatureState.Signature.Instruction;
                            
                            if (!name2traces.ContainsKey(key))
                                name2traces[key] = new List<Example>();
                                
                            name2traces[key].Add(demo);
                        }
                    }
                    else
                    {
                        ExecutionState.EndTrace(); // Clear trace on failure
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error processing example {example}");
                    ExecutionState.EndTrace();
                }
            }

            // 4. Inject Demos into Student
            // We need to traverse the student module and find Predictors matching the signatures we collected traces for.
            
            InjectDemosRecursively(studentClone, name2traces);

            _logger?.LogInformation("Compilation finished.");
            return studentClone;
        }

        private void InjectDemosRecursively(Module module, Dictionary<string, List<Example>> traces)
        {
            // Check if this module is a Predictor
            // Using reflection to find the "State" property which holds SignatureState
            var prop = module.GetType().GetProperty("State");
            if (prop != null && prop.GetValue(module) is SignatureState state)
            {
                var key = state.Signature.Instruction;
                if (traces.ContainsKey(key))
                {
                    var candidates = traces[key];
                    // Take up to MaxBootstrappedDemos
                    var toAdd = candidates.Take(_maxBootstrappedDemos).ToList();
                    
                    state.Demos.AddRange(toAdd);
                    _logger?.LogInformation($"Added {toAdd.Count} demos to module with instruction '{key.Substring(0, 20)}...'");
                }
            }

            // TODO: Traverse sub-modules if Module had children. 
            // In current simple architecture, we assume flat modules or manual composition.
            // If we implement 'Module' with a 'Forward' that calls other modules, we need recursion here.
            // Since Module.cs doesn't maintain a list of children yet, we skip recursion for now.
        }
    }
}