// RealExampleIntegrationTests/OptimizationTests.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using Xunit;

namespace RealExampleIntegrationTests
{
    // Сигнатура для классификации намерений пользователя бота
    [DspInstruction("Classify the user intent into one of the following categories: 'CreatePost', 'CheckBalance', 'Help', 'Unknown'.")]
    public class IntentSignature : IDSpySignature
    {
        [DspInput(Prefix = "User Input:", Description = "Text message from user.")]
        public string Input { get; set; }

        [DspOutput(Prefix = "Intent:", Description = "Category of the intent.")]
        public string Intent { get; set; }
    }

    public class OptimizationTests : BaseIntegrationTest
    {
        public OptimizationTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task BootstrapFewShot_OptimizesIntentClassification()
        {
            var lm = GetRouterAiLM();

            // 1. Create Student (Zero-shot)
            var student = new Predict<IntentSignature>(lm, _logger);

            // 2. Create Teacher (Also Zero-shot or same LM for simplicity here)
            var teacher = new Predict<IntentSignature>(lm, _logger);

            // 3. Trainset (Examples of correct behavior)
            var trainset = new List<Example>
            {
                Example.From(("Input", "Напиши пост про биткоин"), ("Intent", "CreatePost")),
                Example.From(("Input", "Сколько у меня токенов?"), ("Intent", "CheckBalance")),
                Example.From(("Input", "Помоги, я запутался"), ("Intent", "Help")),
                Example.From(("Input", "Привет, как дела?"), ("Intent", "Unknown")),
                Example.From(("Input", "Сделай обзор на iPhone"), ("Intent", "CreatePost"))
            };

            // 4. Metric (Exact Match)
            // Note: RouterAI might return "CreatePost." with dot or whitespace, needs trimming
            Metric exactMatch = (gold, pred) => 
            {
                var g = gold.Get<string>("Intent").Trim();
                var p = pred.Get<string>("Intent")?.Trim().Trim('.');
                return string.Equals(g, p, StringComparison.OrdinalIgnoreCase);
            };

            // 5. Run Optimizer
            _output.WriteLine("Starting Bootstrap Optimization...");
            var optimizer = new BootstrapFewShot<Predict<IntentSignature>>(
                metric: exactMatch, 
                teacher: teacher, 
                maxBootstrappedDemos: 2, // Small amount for speed
                logger: _logger
            );

            var compiled = await optimizer.CompileAsync(student, trainset);

            _output.WriteLine($"Optimization complete. Demos added: {compiled.State.Demos.Count}");
            
            // 6. Verify Demos
            Assert.True(compiled.State.Demos.Count > 0, "Optimizer should have found at least one valid example to bootstrap.");

            // 7. Test on unseen data
            var testInput = "Баланс проверить хочу";
            var result = await compiled.InvokeAsync(new { Input = testInput });
            var prediction = (Prediction)result;

            _output.WriteLine($"Input: {testInput}");
            _output.WriteLine($"Predicted Intent: {prediction.Get<string>("Intent")}");

            // Expecting CheckBalance because of few-shot learning context
            Assert.Contains("CheckBalance", prediction.Get<string>("Intent"));
        }
    }
}