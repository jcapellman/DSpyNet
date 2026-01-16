// DSpyNet.Tests/Teleprompters/Test_BootstrapFewShot.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Clients;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using Xunit;

namespace DSpyNet.Tests.Teleprompters
{
    // Define a simple QA Signature
    [DspInstruction("Answer the question.")]
    public class QASig : IDSpySignature
    {
        [DspInput(Prefix = "Question:")]
        public string Question { get; set; }

        [DspOutput(Prefix = "Answer:")]
        public string Answer { get; set; }
    }

    public class Test_BootstrapFewShot
    {
        [Fact]
        public async Task Test_Compile_AddsDemos()
        {
            // Arrange
            // 1. Dataset
            var trainset = new List<Example>
            {
                Example.From(("Question", "2+2?"), ("Answer", "4")), // Easy
                Example.From(("Question", "Capital of France?"), ("Answer", "Paris")) // Harder
            };

            // 2. DummyLM behavior
            // For the first call (2+2), return "4" (Correct) -> Should be added to demos
            // For the second call (France), return "London" (Incorrect) -> Should NOT be added
            var responses = new List<string> { "4", "London" };
            var lm = new DummyLM(responses);

            // 3. Modules
            var student = new Predict<QASig>(lm);
            var teacher = new Predict<QASig>(lm);

            // 4. Metric (Exact Match)
            Metric exactMatch = (gold, pred) => 
                gold.Get<string>("Answer") == pred.Get<string>("Answer");

            // 5. Optimizer
            var optimizer = new BootstrapFewShot<Predict<QASig>>(exactMatch, teacher: teacher);

            // Act
            var compiledStudent = await optimizer.CompileAsync(student, trainset);

            // Assert
            // The student's state should now have 1 demo (the successful one: 2+2=4)
            Assert.Single(compiledStudent.State.Demos);
            
            var demo = compiledStudent.State.Demos[0];
            Assert.Equal("2+2?", demo.Get<string>("Question"));
            Assert.Equal("4", demo.Get<string>("Answer"));
            
            // Check that the second example was processed but rejected
            Assert.Equal(2, lm.History.Count);
        }
    }
}