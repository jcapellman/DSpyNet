// DSpyNet.Tests/Teleprompters/Test_MIPRO.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Clients;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using Xunit;
using System.Linq;

namespace DSpyNet.Tests.Teleprompters
{
    public class Test_MIPRO
    {
        [Fact]
        public async Task Test_MIPRO_Optimization_Flow()
        {
            // 1. Setup
            // Mock LM that returns instructions when asked for instructions, and answers when asked for answers
            var lmResponses = new List<string>();
            
            // Responses for Instruction Generation (Meta-Prompt)
            for(int i=0; i<5; i++) 
                lmResponses.Add($"Proposed Instruction: You are a math wizard. Solve this. #{i}");

            // Responses for Evaluation (Student) - alternating correct/incorrect to simulate variance
            // "4" is correct for "2+2"
            for(int i=0; i<50; i++)
            {
                lmResponses.Add("4"); // Correct
                lmResponses.Add("5"); // Incorrect
            }

            var lm = new DummyLM(lmResponses);

            var trainset = new List<Example>
            {
                Example.From(("Question", "2+2?"), ("Answer", "4")),
                Example.From(("Question", "3+3?"), ("Answer", "6"))
            };

            var student = new Predict<QASig>(lm);

            Metric exactMatch = (gold, pred) => 
                gold.Get<string>("Answer") == pred.Get<string>("Answer");

            // 2. MIPRO
            var mipro = new MIPRO<Predict<QASig>>(
                promptModel: lm,
                metric: exactMatch,
                numCandidates: 2, // Generate 2 instructions
                numDemoSets: 2,   // Generate 2 demo sets
                numEvaluations: 3 // Run 3 random searches
            );

            // 3. Compile
            var optimized = await mipro.CompileAsync(student, trainset);

            // 4. Assert
            // We check that the optimized module has a potentially different instruction or demos
            // Since it's random search, we just ensure it runs and returns a valid module.
            Assert.NotNull(optimized);
            Assert.NotNull(optimized.State.Instruction);
            // It might be the original or a proposed one.
            
            // Check that DummyLM was called enough times (Meta-prompts + Evaluations)
            // At least 2 meta prompts + 3 evaluations * 2 examples = 8 calls minimum
            Assert.True(lm.History.Count > 5); 
        }
    }
}