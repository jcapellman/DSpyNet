// DSpyNet/DSPy.Modules/ChainOfThought.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// ChainOfThought module. 
    /// Injects a Reasoning step before the main answer.
    /// </summary>
    public class ChainOfThought<TSignature> : Predict<TSignature> where TSignature : IDSpySignature, new()
    {
        // Используем специальный адаптер для CoT
        private new readonly CoTAdapter _adapter;

        public ChainOfThought(ILM lm, ILogger logger = null) : base(lm, logger)
        {
            _adapter = new CoTAdapter();
        }

        public override async Task<Prediction> ForwardAsync(Example input, CancellationToken cancellationToken = default)
        {
            var cotState = State.Clone();

            // 1. Generate Prompt with CoT Trigger
            var promptWithCoT = _adapter.Format(cotState, input);

            _logger?.LogDebug($"[DSPy CoT] Prompt:\n{promptWithCoT}");

            // 2. Invoke LLM
            var responseText = await _lm.GenerateAsync(promptWithCoT, cancellationToken: cancellationToken);

            _logger?.LogDebug($"[DSPy CoT] Response:\n{responseText}");

            // 3. Parse using CoT logic (extracts Reasoning + Standard Fields)
            var prediction = _adapter.Parse(responseText, cotState);

            if (ExecutionState.IsTracing)
            {
                ExecutionState.AddEntry(new TraceEntry
                {
                    SignatureState = State.Clone(), 
                    Inputs = input,
                    Outputs = prediction,
                    PromptUsed = promptWithCoT
                });
            }

            return prediction;
        }

        public override Module DeepClone()
        {
            var clone = (ChainOfThought<TSignature>)this.MemberwiseClone();
            clone.State = this.State.Clone();
            return clone;
        }
    }

    /// <summary>
    /// Specialized Adapter for ChainOfThought.
    /// Handles the injection of the Reasoning prompt and parsing of the reasoning field
    /// which is not present in the static Signature type.
    /// </summary>
    public class CoTAdapter : DSPyAdapter
    {
        private const string REASONING_FIELD = "Reasoning";
        private const string REASONING_PREFIX = "Reasoning:";
        private const string REASONING_TRIGGER = "Reasoning: Let's think step by step.";

        public override string Format(SignatureState state, Example inputs)
        {
            // 1. Get base format (which includes instruction, demos, input, and usually ends with the first output prefix)
            var basePrompt = base.Format(state, inputs);
            
            // 2. Inject CoT Trigger
            // We want to remove the standard "FirstOutput:" suffix added by the base adapter
            // and replace it with our CoT trigger.
            var firstOutput = state.Signature.OutputFields.FirstOrDefault();
            
            if (firstOutput != null && basePrompt.TrimEnd().EndsWith(firstOutput.Prefix))
            {
                var trimmed = basePrompt.TrimEnd();
                // Remove the prefix
                trimmed = trimmed.Substring(0, trimmed.Length - firstOutput.Prefix.Length).TrimEnd();
                
                // Add CoT trigger. 
                // IMPORTANT: We do NOT add the firstOutput.Prefix back yet. 
                // We want the LLM to generate reasoning first.
                return $"{trimmed}\n{REASONING_TRIGGER}";
            }
            
            // Fallback
            return $"{basePrompt}\n{REASONING_TRIGGER}";
        }

        public override Prediction Parse(string llmResponse, SignatureState state)
        {
            // Logic: The response is expected to start with the Reasoning text, 
            // followed by the actual fields (e.g. "Answer: ...")
            
            // We construct a virtual list of fields: [Reasoning, ...OriginalOutputFields]
            var virtualFields = new List<SignatureField>();
            
            // Add Reasoning Field
            virtualFields.Add(new SignatureField 
            { 
                Name = REASONING_FIELD, 
                Prefix = REASONING_PREFIX 
            });
            
            // Add Original Fields
            virtualFields.AddRange(state.Signature.OutputFields);

            // Now we use the same regex logic as base DSPyAdapter, but with our virtual fields list.
            
            var result = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(llmResponse)) return new Prediction(result);

            // Normalize response: 
            // Since we ended prompt with "Reasoning: ...", the LLM output is the value of Reasoning.
            // But our parser expects "Prefix: Value".
            // So we prepend the Reasoning Prefix to the response to make it standard.
            
            // However, the prompt actually ended with "Reasoning: Let's think step by step."
            // The LLM response continues from there. 
            // So the full "Virtual Response" is "Reasoning: Let's think step by step. " + llmResponse.
            // But usually we just want the content. 
            
            // Let's prepend "Reasoning: " to the response to make the Regex parser happy, 
            // assuming the LLM output starts immediately with the reasoning text.
            string textToParse = $"{REASONING_PREFIX} {llmResponse}";

            var markers = virtualFields.Select(f => f.Prefix).ToList();

            for (int i = 0; i < virtualFields.Count; i++)
            {
                var currentField = virtualFields[i];
                var currentMarker = currentField.Prefix;
                var nextMarker = (i + 1 < virtualFields.Count) ? virtualFields[i + 1].Prefix : null;

                var escCurrent = Regex.Escape(currentMarker);
                
                string pattern;
                if (nextMarker != null)
                {
                    var escNext = Regex.Escape(nextMarker);
                    // Match current marker, capture until next marker
                    pattern = $@"{escCurrent}\s*(.*?)\s*(?={escNext}|$)";
                }
                else
                {
                    // Last marker, capture until end
                    pattern = $@"{escCurrent}\s*(.*)";
                }

                var match = Regex.Match(textToParse, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var value = match.Groups[1].Value.Trim();
                    
                    // For Reasoning, we might want to strip the "Let's think step by step." boilerplate if it got caught,
                    // but usually it's in the prompt, not response.
                    
                    result[currentField.Name] = value;
                }
            }

            return new Prediction(result);
        }
    }
}