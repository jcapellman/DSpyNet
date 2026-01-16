// DSpyNet.Examples/Showcases/1_Basic_Sentiment.cs
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.Examples.Config;
using DSpyNet.Examples.Core;
using Microsoft.Extensions.Logging;

namespace DSpyNet.Examples.Showcases
{
    // 1. Определяем Сигнатуру (Контракт)
    [DspInstruction("Analyze the sentiment of the user's review. Classify it as Positive, Negative, or Neutral.")]
    public class SentimentSignature : IDSpySignature
    {
        [DspInput(Prefix = "Review:", Description = "The text to analyze.")]
        public string Review { get; set; }

        [DspOutput(Prefix = "Sentiment:", Description = "The classification result.")]
        public string Sentiment { get; set; }
    }

    public class BasicSentimentExample : ExampleRunner
    {
        public override string Name => "Basic Predict";
        public override string Description => "Simple Input -> Output mapping (Sentiment Analysis).";

        public BasicSentimentExample(AppConfig config, ILoggerFactory loggerFactory) : base(config, loggerFactory) { }

        public override async Task RunAsync()
        {
            PrintHeader("Running Basic Sentiment Analysis");

            // 2. Создаем Модуль (Predictor)
            var predictor = new Predict<SentimentSignature>(_lm, _logger);

            // 3. Данные для теста
            var reviews = new[]
            {
                "This product is amazing! I love it.",
                "Terrible service, never coming back.",
                "It was okay, nothing special."
            };

            foreach (var review in reviews)
            {
                PrintInput("Review", review);

                // 4. Запуск
                var result = await predictor.InvokeAsync(new { Review = review });
                var prediction = (Prediction)result;

                PrintOutput("AI Sentiment", prediction.Get<string>("Sentiment"));
                Console.WriteLine();
            }
        }
    }
}