// DSpyNet/DSPy.Execution/DSPyAdapter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Execution
{
    /// <summary>
    /// Handles converting Signature+State+Inputs into a Prompt string, 
    /// and parsing the LLM response back into an Output object using Regex.
    /// </summary>
    public class DSPyAdapter
    {
        public virtual string Format(SignatureState state, Example inputs)
        {
            var sb = new StringBuilder();

            // 1. Instruction
            if (!string.IsNullOrWhiteSpace(state.Instruction))
            {
                sb.AppendLine(state.Instruction);
                sb.AppendLine();
            }

            // 2. Field Descriptions
            sb.AppendLine("Follow the following format.");
            foreach (var field in state.Signature.InputFields)
            {
                sb.AppendLine($"{field.Prefix} {field.Description}");
            }
            foreach (var field in state.Signature.OutputFields)
            {
                sb.AppendLine($"{field.Prefix} {field.Description}");
            }
            sb.AppendLine();

            // 3. Demos (Few-Shot)
            foreach (var demo in state.Demos)
            {
                sb.AppendLine("---");
                AppendExample(sb, state.Signature, demo);
                sb.AppendLine();
            }

            // 4. Current Input
            sb.AppendLine("---");
            AppendExample(sb, state.Signature, inputs, includeOutputs: false);
            
            // 5. Trigger for the first output field
            var firstOutput = state.Signature.OutputFields.FirstOrDefault();
            if (firstOutput != null)
            {
                sb.Append(firstOutput.Prefix);
            }

            return sb.ToString();
        }

        private void AppendExample(StringBuilder sb, Signature sig, Example ex, bool includeOutputs = true)
        {
            foreach (var input in sig.InputFields)
            {
                var val = ex[input.Name];
                sb.AppendLine($"{input.Prefix} {val}");
            }

            if (includeOutputs)
            {
                // Support CoT demos that have Reasoning
                if (ex["Reasoning"] != null)
                {
                    sb.AppendLine($"Reasoning: {ex["Reasoning"]}");
                }

                foreach (var output in sig.OutputFields)
                {
                    var val = ex[output.Name];
                    sb.AppendLine($"{output.Prefix} {val}");
                }
            }
        }

        public virtual Prediction Parse(string llmResponse, SignatureState state)
        {
            var result = new Dictionary<string, object>();
            
            if (string.IsNullOrWhiteSpace(llmResponse))
                return new Prediction(result);

            var outputFields = state.Signature.OutputFields;
            if (outputFields.Count == 0) return new Prediction(result);

            // Prepare list of markers to look for
            var markers = outputFields.Select(f => f.Prefix).ToList();

            // Handle the case where the prompt ends with the first prefix.
            // The LLM response typically starts with the VALUE, not the prefix.
            // We prepend the first prefix to normalize parsing.
            var firstPrefix = markers.First();
            string textToParse = llmResponse;
            
            if (!textToParse.TrimStart().StartsWith(firstPrefix, StringComparison.OrdinalIgnoreCase))
            {
                textToParse = $"{firstPrefix} {textToParse}";
            }

            for (int i = 0; i < outputFields.Count; i++)
            {
                var currentField = outputFields[i];
                var currentMarker = currentField.Prefix;
                var nextMarker = (i + 1 < outputFields.Count) ? outputFields[i + 1].Prefix : null;

                var escCurrent = Regex.Escape(currentMarker);
                
                string pattern;
                if (nextMarker != null)
                {
                    var escNext = Regex.Escape(nextMarker);
                    // Match content between current marker and next marker
                    pattern = $@"{escCurrent}\s*(.*?)\s*(?={escNext}|$)";
                }
                else
                {
                    // Match content until end of string
                    pattern = $@"{escCurrent}\s*(.*)";
                }

                var match = Regex.Match(textToParse, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    result[currentField.Name] = match.Groups[1].Value.Trim();
                }
            }

            return new Prediction(result);
        }
    }
}