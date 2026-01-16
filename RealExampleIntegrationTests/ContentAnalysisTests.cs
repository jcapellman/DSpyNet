// RealExampleIntegrationTests/ContentAnalysisTests.cs
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using Xunit;
using Xunit.Abstractions;

namespace RealExampleIntegrationTests
{
    // Сигнатура для анализа тональности и безопасности контента (Content Guard)
    [DspInstruction("Analyze the user message. Determine its sentiment and whether it is safe to post.")]
    public class ContentGuardSignature : IDSpySignature
    {
        [DspInput(Prefix = "Message:", Description = "The user's message to analyze.")]
        public string Message { get; set; }

        [DspOutput(Prefix = "Sentiment:", Description = "Positive, Negative, or Neutral.")]
        public string Sentiment { get; set; }

        [DspOutput(Prefix = "Is Safe:", Description = "True if the content is safe, False otherwise.")]
        public string IsSafe { get; set; } // LLM returns string "True"/"False" usually
    }

    public class ContentAnalysisTests : BaseIntegrationTest
    {
        public ContentAnalysisTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task ChainOfThought_AnalyzeMessage_ShouldProvideReasoning()
        {
            // 1. Setup LM
            var lm = GetRouterAiLM();

            // 2. Create ChainOfThought (CoT adds reasoning step automatically)
            var guard = new ChainOfThought<ContentGuardSignature>(lm, _logger);

            // 3. Define Input (Ambiguous message)
            var message = "I hate it when my code doesn't compile! It makes me want to scream.";

            // 4. Run
            var result = await guard.InvokeAsync(new { Message = message });
            var prediction = (Prediction)result;

            // 5. Output results
            var reasoning = prediction.Get<string>("Reasoning");
            var sentiment = prediction.Get<string>("Sentiment");
            var isSafe = prediction.Get<string>("IsSafe");

            _output.WriteLine($"Message: {message}");
            _output.WriteLine($"Reasoning: {reasoning}");
            _output.WriteLine($"Sentiment: {sentiment}");
            _output.WriteLine($"Is Safe: {isSafe}");

            // 6. Assert
            Assert.NotNull(reasoning);
            Assert.Contains("Negative", sentiment); // Expect negative sentiment
            Assert.Contains("True", isSafe); // Safe to post (just complaining)
        }
    }
}