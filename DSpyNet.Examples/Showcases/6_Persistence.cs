// DSpyNet.Examples/Showcases/6_Persistence.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using DSpyNet.Examples.Config;
using DSpyNet.Examples.Core;
using Microsoft.Extensions.Logging;

namespace DSpyNet.Examples.Showcases
{
    [DspInstruction("You are a helpful math tutor. Solve the math problem step by step.")]
    public class MathTutorSignature : IDSpySignature
    {
        [DspInput(Prefix = "Question:", Description = "The math problem.")]
        public string Question { get; set; }

        [DspOutput(Prefix = "Answer:", Description = "The final result.")]
        public string Answer { get; set; }
    }

    public class PersistenceExample : ExampleRunner
    {
        public override string Name => "Persistence (Save/Load)";
        public override string Description => "Compiling a module, saving it to JSON, and loading it back for production.";

        public PersistenceExample(AppConfig config, ILoggerFactory loggerFactory) : base(config, loggerFactory) { }

        public override async Task RunAsync()
        {
            PrintHeader("Running Persistence Example");
            PrintInfo("We will optimize a Math Bot, save it to disk, and then revive it in a fresh instance.");

            // 1. Подготовка данных
            var trainset = new List<Example>
            {
                Example.From(("Question", "2 + 2"), ("Answer", "4")),
                Example.From(("Question", "10 * 5"), ("Answer", "50")),
                Example.From(("Question", "100 / 4"), ("Answer", "25")),
            };

            // 2. Метрика (простая проверка вхождения)
            Metric metric = (gold, pred) => 
                pred.Get<string>("Answer").Contains(gold.Get<string>("Answer"));

            // 3. Создаем "Студента"
            var student = new ChainOfThought<MathTutorSignature>(_lm, _logger);

            // 4. Обучаем (Bootstrap)
            PrintInfo("Bootstrapping demos...");
            var optimizer = new BootstrapFewShot<ChainOfThought<MathTutorSignature>>(
                metric: metric, 
                maxBootstrappedDemos: 2
            );

            var compiledBot = await optimizer.CompileAsync(student, trainset);
            
            PrintInfo($"Optimization done. Demos created: {compiledBot.State.Demos.Count}");

            // 5. СОХРАНЕНИЕ
            string filename = "math_tutor.json";
            PrintInfo($"Saving module state to '{filename}'...");
            await compiledBot.SaveAsync(filename);

            if (File.Exists(filename))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Success] File '{filename}' created.");
                Console.WriteLine($"File content preview: {File.ReadAllText(filename).Substring(0, 100)}...");
                Console.ResetColor();
            }

            // 6. ЗАГРУЗКА (Симуляция продакшена)
            Console.WriteLine("\n--- 🏭 SIMULATING PRODUCTION ENVIRONMENT ---");
            PrintInfo("Creating a fresh, empty module...");
            
            var productionBot = new ChainOfThought<MathTutorSignature>(_lm, _logger);
            
            // Проверяем, что он пустой
            Console.WriteLine($"Fresh bot demos count: {productionBot.State.Demos.Count} (Expected: 0)");

            PrintInfo($"Loading state from '{filename}'...");
            await productionBot.LoadAsync(filename);

            // Проверяем, что демо загрузились
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Success] Loaded bot demos count: {productionBot.State.Demos.Count}");
            Console.ResetColor();

            // 7. Тест загруженного бота
            var question = "If I buy 3 apples for $2 each, how much do I pay?";
            PrintInput("User", question);

            var result = await productionBot.InvokeAsync(new { Question = question });
            var pred = (Prediction)result;

            PrintOutput("Reasoning (Restored CoT)", pred.Get<string>("Reasoning"));
            PrintOutput("Answer", pred.Get<string>("Answer"));

            // Чистим за собой
            try { File.Delete(filename); } catch { }
        }
    }
}