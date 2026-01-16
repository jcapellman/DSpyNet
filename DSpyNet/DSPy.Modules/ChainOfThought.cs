// DSpyNet/DSPy.Modules/ChainOfThought.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;
using System.Collections.Generic;
using System.Text;

namespace DSpyNet.DSPy.Modules
{
    public class ChainOfThought<TSignature> : Predict<TSignature> where TSignature : IDSpySignature, new()
    {
        public ChainOfThought() : base() { }

        public ChainOfThought(ILM lm, ILogger logger = null) : base(lm, logger)
        {
        }

        public override async Task<Prediction> ForwardAsync(Example input)
        {
            var cotState = State.Clone();
            var basePrompt = _adapter.Format(cotState, input);
            var firstOutput = State.Signature.OutputFields.FirstOrDefault();
            
            string promptWithCoT;
            if (firstOutput != null && basePrompt.EndsWith(firstOutput.Prefix))
            {
                var trimmed = basePrompt.Substring(0, basePrompt.Length - firstOutput.Prefix.Length);
                promptWithCoT = trimmed + "Reasoning: Let's think step by step.\n" + firstOutput.Prefix;
            }
            else
            {
                promptWithCoT = basePrompt + "\nReasoning: Let's think step by step.";
            }

            _logger?.LogDebug($"[DSPy CoT] Prompt:\n{promptWithCoT}");

            var responseText = await _lm.GenerateAsync(promptWithCoT);
            _logger?.LogDebug($"[DSPy CoT] Response:\n{responseText}");

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