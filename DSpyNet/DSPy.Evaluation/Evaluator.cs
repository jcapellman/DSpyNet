// DSPy.Evaluation/Evaluator.cs

using System.Collections.Concurrent;
using System.Threading;
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
            Metric metric,
            CancellationToken cancellationToken = default)
        {
            int correct = 0;
            int total = dataset.Count;
            var exceptions = new ConcurrentBag<Exception>();

            var options = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

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

                    var predictionObj = await program.InvokeAsync(example, token);
                    
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

        /// <summary>
        /// Runs the module over the dataset and returns per-example scores in [0,1] using a FeedbackMetric.
        /// Each slot in the returned array corresponds to <c>dataset[i]</c>. Exceptions yield <paramref name="failureScore"/>.
        /// <paramref name="predName"/> is forwarded to the metric (null = module-level).
        /// </summary>
        public async Task<double[]> EvaluatePerExampleAsync(
            Module program,
            List<Example> dataset,
            FeedbackMetric metric,
            double failureScore = 0.0,
            string predName = null,
            CancellationToken cancellationToken = default)
        {
            var scores = new double[dataset.Count];
            var options = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };
            var indexed = dataset.Select((ex, i) => (ex, i));

            _logger?.LogInformation($"Starting per-example evaluation on {dataset.Count} examples...");

            await Parallel.ForEachAsync(indexed, options, async (item, token) =>
            {
                try
                {
                    var predictionObj = await program.InvokeAsync(item.ex, token);
                    if (predictionObj is not Prediction p)
                        throw new InvalidCastException("Program did not return a Prediction object.");

                    var result = metric(item.ex, p, predName);
                    scores[item.i] = result.Score;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error evaluating example {item.i}.");
                    scores[item.i] = failureScore;
                }
            });

            return scores;
        }
    }
}