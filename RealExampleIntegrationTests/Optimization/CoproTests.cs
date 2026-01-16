// RealExampleIntegrationTests/Optimization/CoproTests.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using Xunit;
using Xunit.Abstractions;

namespace RealExampleIntegrationTests.Optimization
{
    // Сигнатура, которую будем оптимизировать
    [DspInstruction("Classify the message.")]
    public class BotIntentSignature : IDSpySignature
    {
        [DspInput(Prefix = "User Message:")]
        public string Message { get; set; }

        [DspOutput(Prefix = "Intent:")]
        public string Intent { get; set; }
    }

    public class CoproTests : BaseIntegrationTest
    {
        public CoproTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Optimize_IntentClassification_WithCOPRO()
        {
            var lm = GetRouterAiLM();

            // 1. Dataset (Hard examples for a bot)
            var trainset = new List<Example>
            {
                // Support
                Example.From(("Message", "Не работает оплата"), ("Intent", "Support")),
                Example.From(("Message", "Позови человека"), ("Intent", "Support")),
                Example.From(("Message", "Бот сломался"), ("Intent", "Support")),
                
                // Content
                Example.From(("Message", "Напиши пост про AI"), ("Intent", "GenerateContent")),
                Example.From(("Message", "Сделай саммари"), ("Intent", "GenerateContent")),
                Example.From(("Message", "Придумай идею"), ("Intent", "GenerateContent")),

                // Balance
                Example.From(("Message", "Сколько денег осталось?"), ("Intent", "CheckBalance")),
                Example.From(("Message", "Тарифы"), ("Intent", "CheckBalance")),

                // ChitChat (Tricky)
                Example.From(("Message", "Привет, как дела?"), ("Intent", "ChitChat")),
                Example.From(("Message", "Ты кто?"), ("Intent", "ChitChat"))
            };

            // 2. Metric: Strict Match
            Metric exactMatch = (gold, pred) =>
            {
                var g = gold.Get<string>("Intent")?.Trim().ToLower();
                var p = pred.Get<string>("Intent")?.Trim().ToLower();
                // Удаляем знаки препинания, если LLM их добавила
                p = p?.Trim('.'); 
                return g == p;
            };

            // 3. Student
            var student = new Predict<BotIntentSignature>(lm, _logger);

            // 4. Configure COPRO
            // Используем небольшие параметры для теста, чтобы не ждать долго и не тратить много денег
            var copro = new COPRO<Predict<BotIntentSignature>>(
                promptModel: lm, // Тот же RouterAI
                metric: exactMatch,
                breadth: 3, // Генерировать 3 кандидата на итерацию
                depth: 1,   // 1 проход оптимизации
                logger: _logger
            );

            _output.WriteLine("🚀 Starting COPRO Optimization...");

            // 5. Run Optimization
            var optimizedProgram = await copro.CompileAsync(student, trainset);

            // 6. Inspect Results
            var newInstruction = optimizedProgram.State.Instruction;
            _output.WriteLine("\n✅ Optimization Finished!");
            _output.WriteLine($"Original Instruction: Classify the message.");
            _output.WriteLine($"Optimized Instruction: {newInstruction}");

            Assert.NotEqual("Classify the message.", newInstruction);

            // 7. Validation Test
            var result = await optimizedProgram.InvokeAsync(new { Message = "У меня проблема с транзакцией" });
            var pred = (Prediction)result;
            _output.WriteLine($"Validation Input: У меня проблема с транзакцией");
            _output.WriteLine($"Predicted Intent: {pred.Get<string>("Intent")}");

            Assert.Contains("Support", pred.Get<string>("Intent"), StringComparison.OrdinalIgnoreCase);
        }
    }
}