// RealExampleIntegrationTests/NewsGenerationTests.cs
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using Xunit;

namespace RealExampleIntegrationTests
{
    // Сигнатура для генерации поста из новости
    [DspInstruction("You are a professional SMM editor. Given a raw news text, create a concise and engaging Telegram post.")]
    public class NewsPostSignature : IDSpySignature
    {
        [DspInput(Prefix = "Raw News:", Description = "The original text of the news article.")]
        public string RawNews { get; set; }

        [DspOutput(Prefix = "Telegram Post:", Description = "A ready-to-publish post with emoji and a clear structure.")]
        public string Post { get; set; }
    }

    public class NewsGenerationTests : BaseIntegrationTest
    {
        public NewsGenerationTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task GeneratePost_FromRawNews_ShouldReturnEngagingText()
        {
            // 1. Setup LM
            var lm = GetRouterAiLM();

            // 2. Create Predictor
            var predictor = new Predict<NewsPostSignature>(lm, _logger);

            // 3. Define Input
            var rawNews = "OpenAI announced GPT-5 with improved reasoning capabilities. It will be available later this year for enterprise users. The model shows 50% better performance in coding tasks.";

            // 4. Run
            var result = await predictor.InvokeAsync(new { RawNews = rawNews });
            var prediction = (Prediction)result;

            // 5. Assert & Output
            var post = prediction.Get<string>("Post");
            _output.WriteLine("=== Generated Post ===");
            _output.WriteLine(post);

            Assert.NotNull(post);
            Assert.Contains("GPT-5", post); // Should keep key entities
            Assert.True(post.Length > 10, "Post should not be empty");
        }
    }
}