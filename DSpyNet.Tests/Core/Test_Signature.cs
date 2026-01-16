// DSpyNet.Tests/Core/Test_Signature.cs
using DSpyNet.DSPy.Core;
using Xunit;

namespace DSpyNet.Tests.Core
{
    [DspInstruction("Given the question, return the answer.")]
    public class QASignature : IDSpySignature
    {
        [DspInput(Description = "The user question", Prefix = "Question:")]
        public string Question { get; set; }

        [DspOutput(Description = "The concise answer", Prefix = "Answer:")]
        public string Answer { get; set; }
    }

    public class Test_Signature
    {
        [Fact]
        public void Test_Signature_Parsing()
        {
            var sig = new Signature(typeof(QASignature));

            Assert.Equal("Given the question, return the answer.", sig.Instruction);
            
            Assert.Single(sig.InputFields);
            Assert.Equal("Question", sig.InputFields[0].Name);
            Assert.Equal("The user question", sig.InputFields[0].Description);
            Assert.Equal("Question:", sig.InputFields[0].Prefix);

            Assert.Single(sig.OutputFields);
            Assert.Equal("Answer", sig.OutputFields[0].Name);
            Assert.Equal("Answer:", sig.OutputFields[0].Prefix);
        }
    }
}