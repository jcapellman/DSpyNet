// DSpyNet/DSPy.Modules/ChainOfThought.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;
using System.Text;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// ChainOfThought module. 
    /// Injects a Reasoning step before the main answer.
    /// </summary>
    public class ChainOfThought<TSignature> : Predict<TSignature> where TSignature : IDSpySignature, new()
    {
        public ChainOfThought(ILM lm, ILogger logger = null) : base(lm, logger)
        {
        }

        public override async Task<Prediction> ForwardAsync(Example input)
        {
            var cotState = State.Clone();
            
            // We use the base DSPyAdapter, but we perform a trick in formatting
            // to include the Reasoning trigger.
            
            // 1. Generate standard prompt
            var basePrompt = _adapter.Format(cotState, input);
            
            // 2. Inject CoT Trigger
            // We look for the last field prefix and insert reasoning before it.
            var firstOutput = State.Signature.OutputFields.FirstOrDefault();
            
            string promptWithCoT;
            if (firstOutput != null && basePrompt.EndsWith(firstOutput.Prefix))
            {
                // Remove the last prefix, insert Reasoning trigger, then add prefix back
                var trimmed = basePrompt.Substring(0, basePrompt.Length - firstOutput.Prefix.Length);
                promptWithCoT = trimmed + "Reasoning: Let's think step by step.\n" + firstOutput.Prefix;
            }
            else
            {
                // Fallback
                promptWithCoT = basePrompt + "\nReasoning: Let's think step by step.";
            }

            _logger?.LogDebug($"[DSPy CoT] Prompt:\n{promptWithCoT}");

            var responseText = await _lm.GenerateAsync(promptWithCoT);

            _logger?.LogDebug($"[DSPy CoT] Response:\n{responseText}");

            // 3. Parse. The updated DSPyAdapter regex parser handles "Reasoning:" automatically if found.
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
}