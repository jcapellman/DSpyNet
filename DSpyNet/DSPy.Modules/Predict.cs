// DSpyNet/DSPy.Modules/Predict.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;
using System.Collections.Generic;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// The standard predictor module. 
    /// Takes a Signature, builds a prompt via Adapter, calls Semantic Kernel, parses output.
    /// </summary>
    /// <typeparam name="TSignature">The C# class defining the Input/Output schema.</typeparam>
    public class Predict<TSignature> : Module where TSignature : IDSpySignature, new()
    {
        // Changed to protected set to allow subclasses (like ChainOfThought) to set it during cloning
        public SignatureState State { get; protected set; }
        protected readonly Kernel _kernel;
        protected readonly DSPyAdapter _adapter;

        public Predict(Kernel kernel, ILogger logger = null) : base(logger)
        {
            _kernel = kernel;
            _adapter = new DSPyAdapter();
            
            // Initialize State from the static Type definition
            var signature = new Signature(typeof(TSignature));
            State = new SignatureState(signature);
        }

        public override async Task<object> InvokeAsync(object input)
        {
            // Support both Example and anonymous/typed objects
            Example inputExample;
            if (input is Example ex)
            {
                inputExample = ex;
            }
            else
            {
                // Convert object properties to Dictionary
                var dict = new Dictionary<string, object>();
                foreach (var prop in input.GetType().GetProperties())
                {
                    dict[prop.Name] = prop.GetValue(input);
                }
                inputExample = new Example(dict);
            }

            return await ForwardAsync(inputExample);
        }

        public virtual async Task<Prediction> ForwardAsync(Example input)
        {
            // 1. Format Prompt
            var prompt = _adapter.Format(State, input);
            
            _logger?.LogDebug($"[DSPy Predict] Prompt generated:\n{prompt}");

            // 2. Call LLM (Semantic Kernel)
            var result = await _kernel.InvokePromptAsync(prompt);
            var responseText = result.GetValue<string>();

            _logger?.LogDebug($"[DSPy Predict] LLM Response:\n{responseText}");

            // 3. Parse Response
            var prediction = _adapter.Parse(responseText, State);

            // 4. Trace
            if (ExecutionState.IsTracing)
            {
                ExecutionState.AddEntry(new TraceEntry
                {
                    SignatureState = State.Clone(), // Snapshot state at this moment
                    Inputs = input,
                    Outputs = prediction,
                    PromptUsed = prompt
                });
            }

            return prediction;
        }

        public override Module DeepClone()
        {
            var clone = (Predict<TSignature>)this.MemberwiseClone();
            clone.State = this.State.Clone(); // Deep clone mutable state
            return clone;
        }
    }
}