// DSpyNet.Examples/Showcases/4_Optimization_COPRO.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using DSpyNet.Examples.Config;
using DSpyNet.Examples.Core;
using Microsoft.Extensions.Logging;

namespace DSpyNet.Examples.Showcases
{
    [DspInstruction("Analyze the text and determine if it is Sarcastic or Genuine.")]
    public class SarcasmSignature : IDSpySignature
    {
        [DspInput(Prefix = "Text:", Description = "The comment or sentence to analyze.")]
        public string Text { get; set; }

        [DspOutput(Prefix = "Label:", Description = "Sarcastic or Genuine.")]
        public string Label { get; set; }
    }

    public class OptimizationCoproExample : ExampleRunner
    {
        public override string Name => "Optimization (COPRO)";
        public override string Description => "Improving Sarcasm Detection by evolving instructions.";

        public OptimizationCoproExample(AppConfig config, ILoggerFactory loggerFactory) : base(config, loggerFactory) { }

        public override async Task RunAsync()
        {
            PrintHeader("Running COPRO: Sarcasm Detector");
            PrintInfo("Sarcasm is hard for AI. A simple instruction often fails.");
            PrintInfo("COPRO will interact with the LLM to propose BETTER instructions based on the dataset.");

            // 1. Студент с базовой (слабой) инструкцией
            var student = new Predict<SarcasmSignature>(_lm, _logger);

            // 2. Датасет (Сложные случаи)
            // Эти примеры сбивают с толку модель без четкой инструкции искать контекст
            var trainset = new List<Example>
            {
                Example.From(("Text", "Oh, great! Another flat tire. Just what I needed."), ("Label", "Sarcastic")),
                Example.From(("Text", "I absolutely love waiting in line at the DMV for 3 hours."), ("Label", "Sarcastic")),
                Example.From(("Text", "This pizza is actually really good."), ("Label", "Genuine")),
                Example.From(("Text", "Thanks for being so helpful by ignoring my email."), ("Label", "Sarcastic")),
                Example.From(("Text", "The weather is nice today."), ("Label", "Genuine")),
                Example.From(("Text", "Wow, you're a genius! (said after dropping a plate)"), ("Label", "Sarcastic"))
            };

            // 3. Метрика (Точное совпадение)
            Metric exactMatch = (gold, pred) => 
            {
                var g = gold.Get<string>("Label")?.Trim().ToLower();
                var p = pred.Get<string>("Label")?.Trim().ToLower().Trim('.');
                return string.Equals(g, p, StringComparison.OrdinalIgnoreCase);
            };

            // 4. Настройка COPRO
            var copro = new COPRO<Predict<SarcasmSignature>>(
                promptModel: _lm,      // Модель, которая будет придумывать инструкции
                metric: exactMatch,    // Как мы меряем успех
                breadth: 3,            // Генерировать 3 кандидата на каждом шаге
                depth: 2,              // 2 итерации улучшения
                logger: _logger
            );

            Console.WriteLine("\n[COPRO] Starting Optimization Loop (this may take a minute)...");
            
            // 5. Запуск
            var compiledProgram = await copro.CompileAsync(student, trainset);

            // 6. Результат
            var originalInstr = "Analyze the text and determine if it is Sarcastic or Genuine.";
            var newInstr = compiledProgram.State.Instruction;

            Console.WriteLine("\n=== Optimization Results ===");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[OLD] {originalInstr}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[NEW] {newInstr}");
            Console.ResetColor();

            // 7. Тест на новом примере
            var testInput = "Yeah, right. Because that makes TOTAL sense.";
            PrintInfo($"\nTesting on: '{testInput}'");
            
            var result = await compiledProgram.InvokeAsync(new { Text = testInput });
            var pred = (Prediction)result;
            
            PrintOutput("Prediction", pred.Get<string>("Label"));
        }
    }
}