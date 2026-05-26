// DSPy.Execution/ExecutionState.cs

using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;

namespace DSpyNet.DSPy.Execution
{
    /// <summary>
    /// Holds the trace of the current execution flow.
    /// Used by Optimizers (BootstrapFewShot) to capture successful traces.
    /// </summary>
    public class TraceEntry
    {
        public IPredictor Predictor { get; set; } // identity, so two preds sharing a signature don't collide
        public SignatureState SignatureState { get; set; }
        public Example Inputs { get; set; }
        public Prediction Outputs { get; set; }
        public string PromptUsed { get; set; }
        public string RawResponse { get; set; } // surfaced to reflection on parse failures
    }

    public static class ExecutionState
    {
        private static readonly AsyncLocal<List<TraceEntry>> _currentTrace = new();
        private static readonly AsyncLocal<bool> _isTracingEnabled = new();

        public static void BeginTrace()
        {
            _currentTrace.Value = new List<TraceEntry>();
            _isTracingEnabled.Value = true;
        }

        public static List<TraceEntry> EndTrace()
        {
            var trace = _currentTrace.Value;
            _currentTrace.Value = null;
            _isTracingEnabled.Value = false;
            return trace;
        }

        public static void AddEntry(TraceEntry entry)
        {
            if (_isTracingEnabled.Value && _currentTrace.Value != null)
            {
                _currentTrace.Value.Add(entry);
            }
        }

        public static bool IsTracing => _isTracingEnabled.Value;
    }
}