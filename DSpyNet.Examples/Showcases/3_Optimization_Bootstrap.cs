// DSpyNet.Examples/Showcases/3_Optimization_Bootstrap.cs
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
    [DspInstruction("Classify the user intent into one of: 'Support', 'Sales', 'Refund', 'TechIssue'.")]
    public class IntentSignature : IDSpySignature
    {
        [DspInput(Prefix = "User Message:")]
        public string Message { get; set; }

        [DspOutput(Prefix = "Intent:")]
        public string Intent { get; set; }
    }

    public class OptimizationExample : ExampleRunner
    {
        public override string Name => "Optimization (BootstrapFewShot)";
        public override string Description => "Self-improving prompt using few-shot learning (Intent Classifier).";

        public OptimizationExample(AppConfig config, ILoggerFactory loggerFactory) : base(config, loggerFactory) { }

        public override async Task RunAsync()
        {
            PrintHeader("Running Optimizer: BootstrapFewShot");
            PrintInfo("We will teach the model to classify complex intents by giving it a few examples.");
            PrintInfo("The optimizer will verify if the model gets them right, and if so, save the 'trace' as a golden example for future prompts.");

            // 1. Создаем студента (Zero-shot)
            var student = new Predict<IntentSignature>(_lm, _logger);

            // 2. Тренировочный сет (Сложные случаи)
            var trainset = new List<Example>
            {
                Example.From(("Message", "Верните мне мои деньги, ничего не работает!"), ("Intent", "Refund")),
                Example.From(("Message", "Сколько стоит ваш премиум план?"), ("Intent", "Sales")),
                Example.From(("Message", "У меня ошибка 404 при входе."), ("Intent", "TechIssue")),
                Example.From(("Message", "Как мне поменять пароль?"), ("Intent", "Support")),
                Example.From(("Message", "Хочу купить лицензию на 5 человек"), ("Intent", "Sales"))
            };

            // 3. Метрика (Точное совпадение)
            Metric exactMatch = (gold, pred) => 
            {
                var g = gold.Get<string>("Intent")?.Trim().ToLower();
                var p = pred.Get<string>("Intent")?.Trim().ToLower();
                // Очистка от знаков препинания
                p = p?.Trim('.')?.Trim(); 
                return g == p;
            };

            // 4. Оптимизатор
            // Учителем может быть более умная модель, но здесь используем саму себя (Self-Correction)
            var teacher = new ChainOfThought<IntentSignature>(_lm, _logger); 
            
            var optimizer = new BootstrapFewShot<Predict<IntentSignature>>(
                metric: exactMatch,
                teacher: teacher,
                maxBootstrappedDemos: 3,
                logger: _logger // Передаем логгер, чтобы видеть процесс
            );

            Console.WriteLine("\n[Optimization] Compiling...");
            var compiledProgram = await optimizer.CompileAsync(student, trainset);
            Console.WriteLine("[Optimization] Done!\n");

            // Проверяем, сколько примеров выучил бот
            int demosCount = compiledProgram.State.Demos.Count;
            PrintInfo($"Optimized program learned {demosCount} few-shot examples.");

            // 5. Тестируем на новых данных
            var testInputs = new[]
            {
                "Куда нажать, чтобы оплатить картой?", // Sales
                "Приложение вылетает при запуске"     // TechIssue
            };

            Console.WriteLine("--- Testing Compiled Model ---");
            foreach (var input in testInputs)
            {
                PrintInput("User", input);
                var result = await compiledProgram.InvokeAsync(new { Message = input });
                var pred = (Prediction)result;
                PrintOutput("Intent", pred.Get<string>("Intent"));
                Console.WriteLine();
            }
        }
    }
}