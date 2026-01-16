// DSpyNet/DSPy.Teleprompters/COPROSignatures.cs
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Teleprompters
{
    [DspInstruction("You are an instruction optimizer for large language models. I will give you a ``signature`` of fields (inputs and outputs) in English. Your task is to propose an instruction that will lead a good language model to perform the task well. Don't be afraid to be creative.")]
    public class BasicGenerateInstruction : IDSpySignature
    {
        [DspInput(Description = "The initial instructions before optimization")]
        public string BasicInstruction { get; set; }

        [DspOutput(Description = "The improved instructions for the language model")]
        public string ProposedInstruction { get; set; }

        [DspOutput(Description = "The string at the end of the prompt, which will help the model start solving the task")]
        public string ProposedPrefixForOutputField { get; set; }
    }

    [DspInstruction("You are an instruction optimizer for large language models. I will give some task instructions I've tried, along with their corresponding validation scores. The instructions are arranged in increasing order based on their scores, where higher scores indicate better quality.\n\nYour task is to propose a new instruction that will lead a good language model to perform the task even better. Don't be afraid to be creative.")]
    public class GenerateInstructionGivenAttempts : IDSpySignature
    {
        [DspInput(Description = "History of attempted instructions and their scores")]
        public string AttemptedInstructions { get; set; }

        [DspOutput(Description = "The improved instructions for the language model")]
        public string ProposedInstruction { get; set; }

        [DspOutput(Description = "The string at the end of the prompt, which will help the model start solving the task")]
        public string ProposedPrefixForOutputField { get; set; }
    }
}