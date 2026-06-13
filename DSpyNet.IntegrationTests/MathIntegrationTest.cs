// DSpyNet.IntegrationTests/MathIntegrationTest.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Clients;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using Microsoft.SemanticKernel;
using Xunit;

namespace DSpyNet.IntegrationTests
{
    // Signature for math reasoning
    [DspInstruction("Solve the math word problem. Think step by step.")]
    public class MathSignature : IDSpySignature
    {
        [DspInput(Prefix = "Question:")]
        public string Question { get; set; }

        [DspOutput(Prefix = "Answer:")]
        public string Answer { get; set; }
    }

    public class MathIntegrationTest
    {
        private readonly ITestOutputHelper _output;

        public MathIntegrationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private Kernel GetKernel()
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                // Skip if no key (for CI/CD without secrets)
                return null;
            }

            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion("gpt-3.5-turbo", apiKey);
            return builder.Build();
        }

        [Fact]
        public async Task Test_Math_ChainOfThought_Bootstrap()
        {
            var kernel = GetKernel();
            if (kernel == null)
            {
                _output.WriteLine("Skipping integration test: OPENAI_API_KEY not found.");
                return;
            }

            // 1. Setup LM
            var lm = kernel.ToDSpyLM();

            // 2. Define Trainset (Hard examples)
            var trainset = new List<Example>
            {
                Example.From(("Question", "If I have 5 apples and eat 2, then buy 3 more, how many do I have?"), ("Answer", "6")),
                Example.From(("Question", "What is 15% of 80?"), ("Answer", "12")),
                Example.From(("Question", "A train travels 60 mph for 2 hours. How far did it go?"), ("Answer", "120 miles"))
            };

            // 3. Define Student (Chain of Thought)
            var student = new ChainOfThought<MathSignature>(lm);

            // 4. Metric (Check if answer is contained in output, fuzzy match)
            Metric containsMetric = (gold, pred) => 
            {
                var p = pred.Get<string>("Answer");
                var g = gold.Get<string>("Answer");
                if (p == null || g == null) return false;
                return p.Contains(g) || g.Contains(p);
            };

            // 5. Bootstrap
            _output.WriteLine("Starting Bootstrap Optimization...");
            var optimizer = new BootstrapFewShot<ChainOfThought<MathSignature>>(containsMetric, maxBootstrappedDemos: 2);
            
            // This will run the logic on real OpenAI
            var compiled = await optimizer.CompileAsync(student, trainset);

            _output.WriteLine($"Optimization done. Demos created: {compiled.State.Demos.Count}");

            // 6. Test on new question
            var result = await compiled.InvokeAsync(new { Question = "I have 10 coins. I lose 3. How many left?" });
            var prediction = (Prediction)result;

            _output.WriteLine("--- Result ---");
            _output.WriteLine($"Reasoning: {prediction.Get<string>("Reasoning")}");
            _output.WriteLine($"Answer: {prediction.Get<string>("Answer")}");

            Assert.NotNull(prediction.Get<string>("Reasoning"));
            Assert.Contains("7", prediction.Get<string>("Answer"));
        }
    }
}