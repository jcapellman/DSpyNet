// DSPy.Evaluation/Evaluator.cs

using System.Collections.Concurrent;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using Microsoft.Extensions.Logging;

namespace DSpyNet.DSPy.Evaluation
{
    public class Evaluator
    {
        private readonly ILogger _logger;
        private readonly int _maxDegreeOfParallelism;

        public Evaluator(ILogger logger = null, int maxDegreeOfParallelism = 4)
        {
            _logger = logger;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        /// <summary>
        /// Runs the module over the dataset and calculates the accuracy based on the metric.
        /// </summary>
        public async Task<double> EvaluateAsync(
            Module program, 
            List<Example> dataset, 
            Metric metric)
        {
            int correct = 0;
            int total = dataset.Count;
            var exceptions = new ConcurrentBag<Exception>();

            var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };

            _logger?.LogInformation($"Starting evaluation on {total} examples...");

            await Parallel.ForEachAsync(dataset, options, async (example, token) =>
            {
                try
                {
                    // Important: We must not share state across threads if the module has mutable runtime state.
                    // However, Module.InvokeAsync usually uses the SignatureState which is conceptually read-only during eval
                    // unless we are doing online learning.
                    // For safety in a real framework, we might want to Clone the program for each thread, 
                    // but for now we assume InvokeAsync is thread-safe regarding the Prompt Template reading.
                    
                    // Note: If 'program' modifies its own Demos list during inference, this will break.
                    // Standard Predict/CoT does not modify state during InvokeAsync.
                    
                    var predictionObj = await program.InvokeAsync(example);
                    
                    Prediction prediction;
                    if (predictionObj is Prediction p)
                        prediction = p;
                    else
                        throw new InvalidCastException("Program did not return a Prediction object.");

                    if (metric(example, prediction))
                    {
                        System.Threading.Interlocked.Increment(ref correct);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error evaluating example.");
                    exceptions.Add(ex);
                }
            });

            if (exceptions.Count > 0)
            {
                _logger?.LogWarning($"Evaluation finished with {exceptions.Count} errors.");
            }

            double score = (double)correct / total * 100.0;
            _logger?.LogInformation($"Evaluation Complete. Score: {score:F2}% ({correct}/{total})");

            return score;
        }
    }
}