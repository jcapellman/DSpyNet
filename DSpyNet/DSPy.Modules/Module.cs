// DSpyNet/DSPy.Modules/Module.cs
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// Base class for all DSPy modules.
    /// Provides cloning capabilities for optimization and naming.
    /// Provides Persistence (Save/Load).
    /// </summary>
    public abstract class Module
    {
        protected readonly ILogger _logger;

        protected Module(ILogger logger = null)
        {
            _logger = logger;
        }

        public abstract Task<object> InvokeAsync(object input, CancellationToken cancellationToken = default);

        public virtual Module DeepClone()
        {
            // Shallow copy memberwise, then deep copy specific mutable fields in subclasses
            var clone = (Module)this.MemberwiseClone();
            return clone;
        }

        /// <summary>
        /// Saves the module state (Instructions, Demos) to a JSON file.
        /// </summary>
        public virtual async Task SaveAsync(string path, CancellationToken cancellationToken = default)
        {
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                IncludeFields = true
            };

            // We serialize the whole object. 
            // Note: Transient services like Kernel, ILogger, ILM will generally be ignored or null in JSON 
            // if they are not properties, or we should mark them JsonIgnore.
            // For Predict<T>, we care about the 'State' property.

            using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, this, this.GetType(), options, cancellationToken);
        }

        /// <summary>
        /// Loads state from a JSON file into the current instance.
        /// </summary>
        public virtual async Task LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Module state file not found", path);

            using var stream = File.OpenRead(path);
            var options = new JsonSerializerOptions { IncludeFields = true };

            // We deserialize into a NEW instance to get the data, then copy relevant state to 'this'.
            var loaded = await JsonSerializer.DeserializeAsync(stream, this.GetType(), options, cancellationToken) as Module;

            if (loaded != null)
            {
                this.CopyStateFrom(loaded);
            }
        }

        /// <summary>
        /// Copies optimizable state (Instructions, Demos) from another module instance.
        /// Subclasses must override to copy their specific state.
        /// </summary>
        protected abstract void CopyStateFrom(Module source);
    }
}