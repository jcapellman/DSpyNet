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
    /// and parsing the LLM response back into an Output object using Regex heuristics.
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
            // Note: Currently SignatureState doesn't store dynamic fields list separate from Signature.
            // In a full implementation, we'd iterate over state.Fields.
            // Since ChainOfThought modifies logic but not the Type, we handle standard OutputFields here.
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
            
            // 5. Trigger for the first output field (or Reasoning if injected)
            // Logic: If ChainOfThought is used, it usually expects "Reasoning:" first.
            // We need a way to detect the "Start" field.
            // For standard Predict, it's the first OutputField.
            
            // Heuristic: Check if inputs has "Reasoning" (unlikely) or if we are in CoT mode.
            // Since Adapter is generic, we look at the Prompt structure. 
            // We append the first output prefix to guide the LLM.
            
            var firstOutput = state.Signature.OutputFields.FirstOrDefault();
            if (firstOutput != null)
            {
                // Simple heuristic: If we are doing CoT, the user usually injects Reasoning manually or via module.
                // Standard DSPy logic: append the prefix of the field we want to generate.
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
                // Check if example has "Reasoning" (CoT) even if not in Signature explicitly
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
            
            // If response is empty
            if (string.IsNullOrWhiteSpace(llmResponse))
                return new Prediction(result);

            var outputFields = state.Signature.OutputFields;
            if (outputFields.Count == 0) return new Prediction(result);

            // Construct Regex to find fields.
            // We are looking for patterns like: "FieldPrefix: Value"
            // The value can be multiline.
            // The lookahead is the next field's prefix or End of String.

            // 1. Identify all prefixes we expect (including Reasoning if CoT)
            var markers = new List<string>();
            // If ChainOfThought was used, we expect "Reasoning:" potentially.
            // We blindly look for it if it appears in the text, or we explicitly look for defined outputs.
            
            // Let's rely on the defined OutputFields.
            foreach(var field in outputFields)
            {
                markers.Add(field.Prefix);
            }

            // Also add "Reasoning:" as a potential marker if we detect CoT style behavior
            // or we just parse it if found.
            bool hasReasoning = llmResponse.Contains("Reasoning:");
            if (hasReasoning && !markers.Contains("Reasoning:"))
            {
                markers.Insert(0, "Reasoning:");
            }

            // 2. Build the parsing logic
            // Since we usually seeded the prompt with the first marker (e.g. "Answer:"), 
            // the LLM response might start immediately with the value, OR it might repeat the prefix.
            
            // Normalize: If the response starts with the value directly (doesn't contain the first prefix at index 0),
            // we prepend the first prefix to make regex parsing uniform.
            var firstExpectedPrefix = markers.FirstOrDefault();
            if (firstExpectedPrefix != null && !llmResponse.TrimStart().StartsWith(firstExpectedPrefix))
            {
                llmResponse = $"{firstExpectedPrefix} {llmResponse}";
            }

            // 3. Iterate markers and extract content between them
            for (int i = 0; i < markers.Count; i++)
            {
                var currentMarker = markers[i];
                var nextMarker = (i + 1 < markers.Count) ? markers[i + 1] : null;

                // Escape markers for Regex
                var escCurrent = Regex.Escape(currentMarker);
                
                string pattern;
                if (nextMarker != null)
                {
                    var escNext = Regex.Escape(nextMarker);
                    // Match current marker, capture everything until the next marker (non-greedy)
                    // DOTALL mode (s) so . matches newlines
                    pattern = $@"{escCurrent}\s*(.*?)\s*(?={escNext}|$)";
                }
                else
                {
                    // Last marker, capture until end
                    pattern = $@"{escCurrent}\s*(.*)";
                }

                var match = Regex.Match(llmResponse, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var value = match.Groups[1].Value.Trim();
                    
                    // Map back to Field Name.
                    // If it's "Reasoning:", it maps to "Reasoning"
                    // If it matches a field prefix, map to field name.
                    string key = null;
                    if (currentMarker == "Reasoning:")
                    {
                        key = "Reasoning";
                    }
                    else
                    {
                        var field = outputFields.FirstOrDefault(f => f.Prefix == currentMarker);
                        if (field != null) key = field.Name;
                    }

                    if (key != null)
                    {
                        result[key] = value;
                    }
                }
            }

            return new Prediction(result);
        }
    }
}