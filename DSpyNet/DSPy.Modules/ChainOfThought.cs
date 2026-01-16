// DSpyNet/DSPy.Modules/ChainOfThought.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;
using System.Collections.Generic;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// ChainOfThought module. 
    /// Adds a "Reasoning" output field dynamically before the actual outputs.
    /// </summary>
    public class ChainOfThought<TSignature> : Predict<TSignature> where TSignature : IDSpySignature, new()
    {
        public ChainOfThought(Kernel kernel, ILogger logger = null) : base(kernel, logger)
        {
            // In the future, we can inject the field into State.Signature.OutputFields here
        }

        public override async Task<Prediction> ForwardAsync(Example input)
        {
            // 1. Clone state to ensure thread safety during execution (and allow temp modification if needed)
            var cotState = State.Clone();
            
            // 2. Use the specialized Adapter that knows how to inject/extract Reasoning
            var cotAdapter = new CoTAdapter();
            
            var prompt = cotAdapter.Format(cotState, input);
            _logger?.LogDebug($"[DSPy CoT] Prompt:\n{prompt}");

            // 3. Invoke LLM
            var result = await _kernel.InvokePromptAsync(prompt);
            var responseText = result.GetValue<string>();
            _logger?.LogDebug($"[DSPy CoT] Response:\n{responseText}");

            // 4. Parse using CoT logic
            var prediction = cotAdapter.Parse(responseText, cotState);
            
            // 5. Trace
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
            var clone = (ChainOfThought<TSignature>)this.MemberwiseClone();
            // This now works because State set accessor is protected in Predict<T>
            clone.State = this.State.Clone(); 
            return clone;
        }
    }

    /// <summary>
    /// Specialized Adapter for ChainOfThought.
    /// Injects "Reasoning" field logic into the prompt and parser.
    /// </summary>
    public class CoTAdapter : DSPyAdapter
    {
        public override string Format(SignatureState state, Example inputs)
        {
            // Base format gives us the standard template
            var originalFormat = base.Format(state, inputs);
            
            // We want to insert "Reasoning: Let's think step by step" before the first output field prefix.
            var firstOutput = state.Signature.OutputFields.FirstOrDefault();
            if (firstOutput == null) return originalFormat;

            // Heuristic: Check if the prompt ends with the first output prefix (which standard adapter does)
            if (originalFormat.EndsWith(firstOutput.Prefix))
            {
                var len = firstOutput.Prefix.Length;
                var trimmed = originalFormat.Substring(0, originalFormat.Length - len);
                
                // Add the CoT trigger
                return trimmed + "Reasoning: Let's think step by step.\n" + firstOutput.Prefix;
            }
            
            return originalFormat + "\nReasoning: Let's think step by step.";
        }

        public override Prediction Parse(string llmResponse, SignatureState state)
        {
            var result = new Dictionary<string, object>();
            
            var firstOutput = state.Signature.OutputFields.FirstOrDefault();
            
            if (firstOutput == null) 
            {
                return new Prediction(new Dictionary<string, object> { { "Raw", llmResponse } });
            }

            var firstMarker = firstOutput.Prefix;
            var markerIdx = llmResponse.IndexOf(firstMarker);

            if (markerIdx != -1)
            {
                // Everything before the first real output marker is Reasoning
                var reasoning = llmResponse.Substring(0, markerIdx).Trim();
                result["Reasoning"] = reasoning;
                
                var restOfResponse = llmResponse.Substring(markerIdx);
                // Move past the prefix to get the value of the first field
                var valueStart = firstMarker.Length;
                
                // Safety check
                if (valueStart > restOfResponse.Length) valueStart = restOfResponse.Length;
                
                var valueText = restOfResponse.Substring(valueStart); 
                
                // Use base parser for the structured part
                var basePrediction = base.Parse(valueText, state);
                
                // Merge results
                foreach (var kvp in basePrediction.ToDictionary())
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                // Fallback: Assume it's reasoning + implicit answer or failed structure
                result["Reasoning"] = llmResponse;
                
                // Try parsing normally just in case
                var fallback = base.Parse(llmResponse, state);
                foreach(var kvp in fallback.ToDictionary())
                {
                    if (!result.ContainsKey(kvp.Key))
                        result[kvp.Key] = kvp.Value;
                }
            }

            return new Prediction(result);
        }
    }
}