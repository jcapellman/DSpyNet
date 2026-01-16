// DSPy.Modules/Module.cs

using Microsoft.Extensions.Logging;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// Base class for all DSPy modules.
    /// Provides cloning capabilities for optimization and naming.
    /// </summary>
    public abstract class Module
    {
        protected readonly ILogger _logger;

        protected Module(ILogger logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Main execution entry point.
        /// In Python this is `forward`, but in C# we use InvokeAsync.
        /// </summary>
        public abstract Task<object> InvokeAsync(object input);

        /// <summary>
        /// Creates a deep copy of the module.
        /// Crucial for optimizers (COPRO/MIPRO) to create candidates.
        /// </summary>
        public virtual Module DeepClone()
        {
            // Using JSON serialization as a robust way to deep clone module state (including SignatureStates)
            // Limitations: Dependencies like ILogger or Kernel won't serialize well.
            // In a real prod scenario, we might need a custom Clone method that re-injects dependencies.
            
            // For this implementation, we assume we recreate the module and copy state manually 
            // if JSON fails, but let's try a serialize/deserialize approach for the state properties.
            
            // NOTE: Since we rely on DI for Kernel/Logger, full JSON clone is dangerous.
            // We delegate to a template method that concrete classes can override, 
            // or we use MemberwiseClone and manually clone the SignatureState.
            
            var clone = (Module)this.MemberwiseClone();
            // Subclasses must ensure their SignatureState is cloned.
            return clone;
        }
    }
}