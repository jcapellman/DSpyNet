// DSpyNet.Examples/Showcases/5_Optimization_MIPRO.cs
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
    // --- SIGNATURES ---

    // 1. Целевая задача: Переписать вопрос пользователя в поисковый запрос
    [DspInstruction("Reformulate the user question into a precise search engine query.")]
    public class RagQuerySignature : IDSpySignature
    {
        [DspInput(Prefix = "User Chat Message:", Description = "Raw input from chat.")]
        public string Message { get; set; }

        [DspInput(Prefix = "Chat History Summary:", Description = "Context of conversation.")]
        public string Context { get; set; }

        [DspOutput(Prefix = "Search Query:", Description = "Keywords optimized for vector search.")]
        public string Query { get; set; }
    }

    // 2. Судья: Оценивает качество поискового запроса (LLM-as-a-Judge)
    [DspInstruction("Evaluate the quality of the search query generated from the user message.")]
    public class QueryJudgeSignature : IDSpySignature
    {
        [DspInput] public string Message { get; set; }
        [DspInput] public string GeneratedQuery { get; set; }
        
        [DspOutput(Prefix = "Score:", Description = "Rate 1 to 5. 5 means query captures all keywords perfectly.")]
        public string Score { get; set; }
    }

    public class OptimizationMiproExample : ExampleRunner
    {
        public override string Name => "Optimization (MIPRO)";
        public override string Description => "Bayesian Optimization for RAG Query Rewriting using LLM-as-a-Judge.";

        public OptimizationMiproExample(AppConfig config, ILoggerFactory loggerFactory) : base(config, loggerFactory) { }

        public override async Task RunAsync()
        {
            PrintHeader("Running MIPRO: RAG Query Optimizer");
            PrintInfo("Task: Convert casual chat messages into high-quality search queries.");
            PrintInfo("Metric: Another LLM acts as a judge to rate the quality (1-5).");
            PrintInfo("MIPRO will verify combinations of Instructions AND Few-Shot Examples using Bayesian Search.");

            // 1. Студент (используем ChainOfThought для лучшего качества)
            var student = new ChainOfThought<RagQuerySignature>(_lm, _logger);

            // 2. Тренировочный сет (Реальные сценарии поддержки)
            var trainset = new List<Example>
            {
                Example.From(
                    ("Message", "Why is my internet slow?"), 
                    ("Context", "User has Fiber 100Mbps plan."),
                    ("Query", "fiber internet slow troubleshooting packet loss latency check")
                ),
                Example.From(
                    ("Message", "How do I pay?"), 
                    ("Context", "User is in the billing section."),
                    ("Query", "payment methods credit card bank transfer invoice billing portal")
                ),
                Example.From(
                    ("Message", "It doesn't work on my phone."), 
                    ("Context", "App crashing on iOS 17."),
                    ("Query", "ios 17 app crash compatibility iphone troubleshooting logs")
                ),
                 Example.From(
                    ("Message", "I want a refund"), 
                    ("Context", "Purchased 2 days ago."),
                    ("Query", "refund policy 14 days money back guarantee procedure")
                )
            };

            // 3. Метрика (LLM Judge)
            // Мы создаем отдельный предиктор для оценки, чтобы не загрязнять основной пайплайн
            var judge = new Predict<QueryJudgeSignature>(_lm);

            Metric judgeMetric = (gold, pred) => 
            {
                // Синхронный вызов внутри метрики (для совместимости с делегатом)
                // В реальном коде лучше использовать async везде, но MIPRO ждет синхронный результат
                var t = Task.Run(async () => 
                {
                    var res = await judge.InvokeAsync(new { 
                        Message = gold.Get<string>("Message"),
                        GeneratedQuery = pred.Get<string>("Query")
                    });
                    return (Prediction)res;
                });
                t.Wait();
                
                var scoreStr = t.Result.Get<string>("Score");
                if (int.TryParse(scoreStr, out int score))
                {
                    return score >= 4; // Успех если оценка 4 или 5
                }
                return false;
            };

            // 4. Запуск MIPRO
            var mipro = new MIPRO<ChainOfThought<RagQuerySignature>>(
                promptModel: _lm,
                metric: judgeMetric,
                numCandidates: 3,       // Сгенерировать 3 варианта инструкций
                numDemoSets: 3,         // Сгенерировать 3 варианта подборок примеров
                numEvaluations: 10,     // 10 итераций байесовского поиска
                maxBootstrappedDemos: 2,
                logger: _logger
            );

            Console.WriteLine("\n[MIPRO] Starting Bayesian Optimization...");
            Console.WriteLine("This will try different instructions and example combinations to maximize the Judge Score.");
            
            var compiledProgram = await mipro.CompileAsync(student, trainset);

            // 5. Результаты
            Console.WriteLine("\n=== Winner Program Configuration ===");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Instruction: {compiledProgram.State.Instruction}");
            Console.WriteLine($"Few-Shot Demos: {compiledProgram.State.Demos.Count}");
            Console.ResetColor();

            // 6. Демо
            var userMsg = "My router has a red light blinking";
            var userCtx = "Router Model X-2000";
            
            PrintInfo($"\nTesting on: '{userMsg}' ({userCtx})");
            
            var result = await compiledProgram.InvokeAsync(new { Message = userMsg, Context = userCtx });
            var pred = (Prediction)result;

            PrintOutput("Reasoning", pred.Get<string>("Reasoning"));
            PrintOutput("Optimized Query", pred.Get<string>("Query"));
        }
    }
}