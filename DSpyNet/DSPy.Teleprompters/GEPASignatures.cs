// DSpyNet/DSPy.Teleprompters/GEPASignatures.cs
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Teleprompters
{
    // Adapted from gepa-ai/gepa src/gepa/strategies/instruction_proposal.py.
    [DspInstruction(
        "I provided an assistant with the following instructions to perform a task for me. " +
        "Below the current instruction you will see per-example records — each with the inputs the predictor received, " +
        "the outputs it generated (or a raw response if parsing failed), and feedback on how the response could be better. " +
        "Your task is to write a new instruction for the assistant. " +
        "Read the inputs carefully and identify the input format and infer a detailed task description. " +
        "Read all the assistant responses and the corresponding feedback. " +
        "Identify all niche and domain-specific factual information about the task and include it in the instruction, since it may not be available to the assistant in the future. " +
        "If the assistant utilized a generalizable strategy to solve the task, include that strategy in the instruction as well. " +
        "Provide the new instructions enclosed in a ``` block.")]
    public class GEPAReflectionSignature : IDSpySignature
    {
        [DspInput(Description = "The current instruction in use")]
        public string CurrentInstruction { get; set; }

        [DspInput(Description = "Name of the predictor being optimized")]
        public string PredictorName { get; set; }

        [DspInput(Description = "Per-example records (inputs, generated outputs, and feedback)")]
        public string Traces { get; set; }

        [DspInput(Description = "Summary of per-example scores")]
        public string Feedback { get; set; }

        [DspOutput(Description = "The improved instruction text, enclosed in a ``` block")]
        public string ImprovedInstruction { get; set; }
    }
}
