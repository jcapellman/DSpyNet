// DSPy.Core/SignatureState.cs

namespace DSpyNet.DSPy.Core
{
    /// <summary>
    /// Mutable state for a predictor. This is what Optimizers modify.
    /// It decouples the static C# class definition (Signature) from the dynamic prompt content.
    /// </summary>
    public class SignatureState
    {
        public Signature Signature { get; }
        
        // Mutable Instruction (can be optimized)
        public string Instruction { get; set; }
        
        // Few-Shot Demonstrations (can be bootstrapped)
        public List<Example> Demos { get; set; }

        public SignatureState(Signature signature)
        {
            Signature = signature;
            Instruction = signature.Instruction;
            Demos = new List<Example>();
        }

        /// <summary>
        /// Creates a deep clone of the state for optimization candidates.
        /// </summary>
        public SignatureState Clone()
        {
            var clone = new SignatureState(Signature)
            {
                Instruction = this.Instruction,
                Demos = new List<Example>(this.Demos) // Shallow copy of examples is usually enough as Example is immutable-ish
            };
            return clone;
        }
    }
}