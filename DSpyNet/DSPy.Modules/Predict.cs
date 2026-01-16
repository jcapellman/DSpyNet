// DSpyNet/DSPy.Modules/Predict.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DSpyNet.DSPy.Modules
{
    public class Predict<TSignature> : Module where TSignature : IDSpySignature, new()
    {
        public SignatureState State { get; protected set; }
        
        [JsonIgnore]
        protected ILM _lm; 
        
        [JsonIgnore]
        protected readonly DSPyAdapter _adapter;

        // Default constructor for JSON Deserialization
        public Predict() : base(null)
        {
            _adapter = new DSPyAdapter();
            var signature = new Signature(typeof(TSignature));
            State = new SignatureState(signature);
        }

        public Predict(ILM lm, ILogger logger = null) : base(logger)
        {
            _lm = lm;
            _adapter = new DSPyAdapter();
            var signature = new Signature(typeof(TSignature));
            State = new SignatureState(signature);
        }

        // Setter for LM to allow injecting it after deserialization or cloning
        public void SetLM(ILM lm)
        {
            _lm = lm;
        }

        public override async Task<object> InvokeAsync(object input)
        {
            Example inputExample;
            if (input is Example ex)
            {
                inputExample = ex;
            }
            else
            {
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
            var prompt = _adapter.Format(State, input);
            _logger?.LogDebug($"[DSPy Predict] Prompt generated:\n{prompt}");

            var responseText = await _lm.GenerateAsync(prompt);
            _logger?.LogDebug($"[DSPy Predict] LLM Response:\n{responseText}");

            var prediction = _adapter.Parse(responseText, State);

            if (ExecutionState.IsTracing)
            {
                ExecutionState.AddEntry(new TraceEntry
                {
                    SignatureState = State.Clone(),
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
            clone.State = this.State.Clone();
            // LM reference is shared
            return clone;
        }

        protected override void CopyStateFrom(Module source)
        {
            if (source is Predict<TSignature> sourcePredict)
            {
                // We overwrite the State content
                this.State.Instruction = sourcePredict.State.Instruction;
                this.State.Demos = new List<Example>(sourcePredict.State.Demos);
                
                // Signature definitions are static/type-based, so we don't need to copy them,
                // but we ensure the State has the reference.
                if (this.State.Signature == null)
                {
                     // Should be initialized by constructor, but safety check
                     this.State.SetSignature(new Signature(typeof(TSignature)));
                }
            }
        }
    }
}