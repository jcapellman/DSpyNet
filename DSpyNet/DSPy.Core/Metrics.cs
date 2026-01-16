// DSPy.Core/Metrics.cs

namespace DSpyNet.DSPy.Core
{
    /// <summary>
    /// Delegate for evaluating a prediction against a gold standard example.
    /// Returns true if the prediction is considered correct (pass).
    /// </summary>
    public delegate bool Metric(Example gold, Prediction prediction);

    /// <summary>
    /// Async version of the metric delegate if needed.
    /// </summary>
    public delegate Task<bool> AsyncMetric(Example gold, Prediction prediction);
}