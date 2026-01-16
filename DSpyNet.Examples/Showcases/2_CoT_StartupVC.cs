// DSpyNet.Examples/Showcases/2_CoT_StartupVC.cs
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.Examples.Config;
using DSpyNet.Examples.Core;
using Microsoft.Extensions.Logging;

namespace DSpyNet.Examples.Showcases
{
    // Сигнатура для сложного анализа
    [DspInstruction("You are a cynical Venture Capitalist. Evaluate the startup idea brutally.")]
    public class StartupEvaluationSignature : IDSpySignature
    {
        [DspInput(Prefix = "Pitch:", Description = "The startup elevator pitch.")]
        public string Pitch { get; set; }

        [DspOutput(Prefix = "Verdict:", Description = "Invest or Pass.")]
        public string Verdict { get; set; }

        [DspOutput(Prefix = "Risk Score:", Description = "A score from 1 (Safe) to 10 (Suicide mission).")]
        public string RiskScore { get; set; }
    }

    public class CoTStartupExample : ExampleRunner
    {
        public override string Name => "Chain of Thought (CoT)";
        public override string Description => "Complex reasoning before answering (VC Pitch Analysis).";

        public CoTStartupExample(AppConfig config, ILoggerFactory loggerFactory) : base(config, loggerFactory) { }

        public override async Task RunAsync()
        {
            PrintHeader("Running VC Pitch Analysis (Chain of Thought)");

            // Используем ChainOfThought вместо обычного Predict
            // Это заставит модель сначала сгенерировать поле "Reasoning", а потом уже Verdict и RiskScore.
            var cot = new ChainOfThought<StartupEvaluationSignature>(_lm, _logger);

            var pitch = "Uber for Dog Walking, but using Drones to hold the leash.";

            PrintInput("Startup Pitch", pitch);
            PrintInfo("Asking AI to think step by step...");

            var result = await cot.InvokeAsync(new { Pitch = pitch });
            var prediction = (Prediction)result;

            // Получаем доступ к автоматически сгенерированному полю Reasoning
            var reasoning = prediction.Get<string>("Reasoning");
            var verdict = prediction.Get<string>("Verdict");
            var risk = prediction.Get<string>("RiskScore");

            Console.WriteLine("\n--- 🧠 AI Thoughts (Reasoning) ---");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(reasoning);
            Console.ResetColor();
            Console.WriteLine("----------------------------------");

            PrintOutput("Verdict", verdict);
            PrintOutput("Risk Score", risk);
        }
    }
}