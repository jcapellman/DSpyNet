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

    /// <summary>
    /// Per-example score in [0,1] plus optional textual feedback used by reflection-based optimizers (GEPA).
    /// </summary>
    public record ScoreFeedback(double Score, string Feedback);

    /// <summary>
    /// Delegate for richer metrics that return a score plus feedback.
    /// <paramref name="predName"/> identifies which predictor in a composite module the call targets;
    /// null means module-level evaluation.
    /// </summary>
    public delegate ScoreFeedback FeedbackMetric(Example gold, Prediction prediction, string predName);

    public static class MetricAdapters
    {
        /// <summary>Wraps a bool Metric as a FeedbackMetric with empty feedback (1.0 on pass, 0.0 on fail).</summary>
        public static FeedbackMetric FromBool(Metric m) =>
            (gold, pred, _) => new ScoreFeedback(m(gold, pred) ? 1.0 : 0.0, string.Empty);
    }
}
