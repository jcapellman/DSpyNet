// DSpyNet.Tests/Teleprompters/Test_GEPA.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Clients;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using Xunit;

namespace DSpyNet.Tests.Teleprompters
{
    public class Test_GEPA
    {
        [Fact]
        public async Task Test_GEPA_Optimization_Flow()
        {
            // Scripted DummyLM responses: student answers and reflection proposals interleave.
            // Pad generously — DummyLM returns "" once exhausted.
            var lmResponses = new List<string>();
            for (int i = 0; i < 100; i++)
            {
                lmResponses.Add("4");
                lmResponses.Add("Solve the arithmetic precisely.");
            }
            var lm = new DummyLM(lmResponses);

            var trainset = new List<Example>
            {
                Example.From(("Question", "2+2?"), ("Answer", "4")),
                Example.From(("Question", "1+3?"), ("Answer", "4")),
                Example.From(("Question", "0+4?"), ("Answer", "4")),
                Example.From(("Question", "2+2?"), ("Answer", "4")),
                Example.From(("Question", "3+1?"), ("Answer", "4"))
            };

            var student = new Predict<QASig>(lm);
            var original = student.State.Instruction;

            FeedbackMetric metric = (gold, pred, predName) =>
            {
                bool ok = gold.Get<string>("Answer") == pred.Get<string>("Answer");
                return new ScoreFeedback(ok ? 1.0 : 0.0, ok ? "correct" : $"expected {gold.Get<string>("Answer")} got {pred.Get<string>("Answer")}");
            };

            var gepa = new GEPA<Predict<QASig>>(
                reflectionLM: lm,
                metric: metric,
                options: new GEPAOptions
                {
                    Auto = GEPAAutoBudget.Light,
                    ReflectionMinibatchSize = 2,
                    ValidationSplit = 0.4,
                    Seed = 7
                });

            var optimized = await gepa.CompileAsync(student, trainset);

            Assert.NotNull(optimized);
            Assert.NotNull(optimized.State.Instruction);
            Assert.True(lm.History.Count > 0);
            // Student is untouched because GEPA operates on clones.
            Assert.Equal(original, student.State.Instruction);
        }
    }
}
