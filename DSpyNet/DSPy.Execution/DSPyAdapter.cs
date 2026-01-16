// DSPy.Execution/DSPyAdapter.cs

using System.Text;
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Execution
{
    /// <summary>
    /// Handles converting Signature+State+Inputs into a Prompt string, 
    /// and parsing the LLM response back into an Output object.
    /// </summary>
    public class DSPyAdapter
    {
        public virtual string Format(SignatureState state, Example inputs)
        {
            var sb = new StringBuilder();

            // 1. Instruction
            sb.AppendLine(state.Instruction);
            sb.AppendLine();

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
            
            // 5. Trigger for the first output field (CoT logic usually handles this, but base Predict needs it too)
            var firstOutput = state.Signature.OutputFields.FirstOrDefault();
            if (firstOutput != null)
            {
                sb.Append(firstOutput.Prefix);
                // No newline, we want the LLM to complete here
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
            var lines = llmResponse.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(l => l.Trim())
                                   .ToList();

            // Very naive parser assuming "Field: Value" format. 
            // In a real implementation, this needs to be more robust against hallucinated newlines.
            
            // Because we prompted with the prefix of the first output field, 
            // the LLM response usually starts with the VALUE of the first field.
            
            var outputFields = state.Signature.OutputFields;
            if (outputFields.Count == 0) return new Prediction(result);

            // We need to reconstruct the full text to parse properly if the LLM output multiline
            // Or if we pre-seeded the prompt with the prefix.
            
            // Strategy: Look for next field markers.
            
            string currentText = llmResponse;
            
            // If the adapter appended the first prefix, the LLM response is just the value.
            // But subsequent fields will have prefixes.
            
            // Simplistic parsing logic:
            // 1. The whole text starts with the first field's value (since we injected the prefix).
            // 2. We scan for the *second* field's prefix.
            
            int currentIndex = 0;
            
            for (int i = 0; i < outputFields.Count; i++)
            {
                var field = outputFields[i];
                var nextField = (i + 1 < outputFields.Count) ? outputFields[i + 1] : null;

                string value = "";

                if (nextField != null)
                {
                    var nextMarker = nextField.Prefix;
                    var nextIdx = currentText.IndexOf(nextMarker, currentIndex);
                    
                    if (nextIdx != -1)
                    {
                        value = currentText.Substring(currentIndex, nextIdx - currentIndex).Trim();
                        currentIndex = nextIdx + nextMarker.Length; // Move past the next marker
                    }
                    else
                    {
                        // Could not find next marker, take rest
                        value = currentText.Substring(currentIndex).Trim();
                        currentIndex = currentText.Length;
                    }
                }
                else
                {
                    // Last field
                    value = currentText.Substring(currentIndex).Trim();
                }

                result[field.Name] = value;
            }

            return new Prediction(result);
        }
    }
}