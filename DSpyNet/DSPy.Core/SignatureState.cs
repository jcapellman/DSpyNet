// DSpyNet/DSPy.Core/SignatureState.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DSpyNet.DSPy.Core
{
    /// <summary>
    /// Mutable state for a predictor. This is what Optimizers modify.
    /// </summary>
    public class SignatureState
    {
        // We don't serialize the Signature Type info directly to JSON easily without custom converters,
        // but we assume the Module structure restores it. 
        // We DO need to save Instruction and Demos.
        [JsonIgnore]
        public Signature Signature { get; private set; }
        
        public string Instruction { get; set; }
        public List<Example> Demos { get; set; }

        // Constructor for JSON Deserialization
        public SignatureState()
        {
            Demos = new List<Example>();
        }

        public SignatureState(Signature signature)
        {
            Signature = signature;
            Instruction = signature.Instruction;
            Demos = new List<Example>();
        }

        public void SetSignature(Signature signature)
        {
            Signature = signature;
            // If Instruction wasn't loaded from JSON (is null), use default
            if (string.IsNullOrEmpty(Instruction))
            {
                Instruction = signature.Instruction;
            }
        }

        public SignatureState Clone()
        {
            var clone = new SignatureState(Signature)
            {
                Instruction = this.Instruction,
                // Shallow copy list of examples
                Demos = new List<Example>(this.Demos) 
            };
            return clone;
        }
    }
}