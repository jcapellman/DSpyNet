// DSpyNet/DSPy.Teleprompters/InstructionSignatures.cs
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Teleprompters
{
    [DspInstruction("You are an instruction optimizer for large language models. I will give you a ``signature`` of fields (inputs and outputs) and some examples. Your task is to propose an instruction that will lead a good language model to perform the task well. Don't be afraid to be creative.")]
    public class GenerateInstructionSignature : IDSpySignature
    {
        [DspInput(Description = "The initial instructions before optimization")]
        public string BasicInstruction { get; set; }

        [DspInput(Description = "Examples of the task (Input -> Output)")]
        public string Examples { get; set; }

        [DspOutput(Description = "The improved instructions for the language model")]
        public string ProposedInstruction { get; set; }
    }
}