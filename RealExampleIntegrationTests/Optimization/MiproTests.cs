// RealExampleIntegrationTests/Optimization/MiproTests.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using Xunit;

namespace RealExampleIntegrationTests.Optimization
{
    // Сигнатура для генерации постов
    [DspInstruction("Convert the news into a telegram post.")]
    public class NewsGenSignature : IDSpySignature
    {
        [DspInput(Prefix = "Source Text:")]
        public string Source { get; set; }

        [DspOutput(Prefix = "Telegram Post:")]
        public string Post { get; set; }
    }

    public class MiproTests : BaseIntegrationTest
    {
        public MiproTests(ITestOutputHelper output) : base(output) { }

        // LLM-as-a-Judge Metric
        private async Task<bool> LlmJudgeMetric(Example gold, Prediction pred)
        {
            var lm = GetRouterAiLM();
            var judgeModule = new Predict<ContentJudgeSignature>(lm);

            var inputRaw = gold.Get<string>("Source");
            var goldPost = gold.Get<string>("Post");
            var predPost = pred.Get<string>("Post");

            // Запускаем судью
            var result = await judgeModule.InvokeAsync(new
            {
                Input = inputRaw,
                Output = predPost,
                Reference = goldPost
            });
            var judgment = (Prediction)result;

            var scoreStr = judgment.Get<string>("Score");
            var reasoning = judgment.Get<string>("Reasoning");

            _output.WriteLine($"[Judge] Score: {scoreStr} | Reasoning: {reasoning}");

            if (int.TryParse(scoreStr, out int score))
            {
                // Требуем высокую оценку
                return score >= 5; 
            }
            return false;
        }

        [Fact]
        public async Task Optimize_NewsGeneration_WithMIPRO_Bayesian()
        {
            var lm = GetRouterAiLM();

            // 1. Trainset
            var trainset = new List<Example>
            {
                Example.From(
                    ("Source", "Apple представила iPhone 16 с новой кнопкой Camera Control и процессором A18. Цены начинаются от $799."),
                    ("Post", "🍎 <b>Apple показала iPhone 16</b>\n\nГлавное нововведение — кнопка Camera Control для управления зумом и фокусом. Внутри стоит мощный чип A18.\n\n💸 Цена: от $799.\n\n#Apple #iPhone16")
                ),
                Example.From(
                    ("Source", "Курс Биткоина пробил отметку в 100 000 долларов на фоне новостей о принятии ETF."),
                    ("Post", "🚀 <b>BTC > $100k!</b>\n\nИсторический момент: первая криптовалюта пробила психологическую отметку. Драйвер роста — принятие ETF.\n\nКак думаете, пойдем выше? 👇\n\n#Crypto #Bitcoin")
                ),
                Example.From(
                    ("Source", "В Telegram вышло обновление: теперь можно дарить подписки Telegram Premium за звезды."),
                    ("Post", "⭐️ <b>Обновление Telegram</b>\n\nТеперь Premium можно дарить за Stars! Дуров продолжает развивать внутреннюю экономику мессенджера.\n\n#Telegram #Update")
                )
            };

            // 2. Student 
            var student = new ChainOfThought<NewsGenSignature>(lm, _logger);

            // 3. Configure MIPRO with Bayesian Optimization
            // Увеличим бюджет, так как Байесу нужно немного данных для разгона
            var mipro = new MIPRO<ChainOfThought<NewsGenSignature>>(
                promptModel: lm,
                metric: (g, p) => LlmJudgeMetric(g, p).Result, 
                numCandidates: 2,       // 2 кандидата инструкций (+1 исходная)
                numDemoSets: 2,         // 2 набора демок (+1 zero-shot)
                numEvaluations: 5,      // 5 итераций оптимизации (в проде ставьте 20-30)
                maxBootstrappedDemos: 1,
                logger: _logger
            );

            _output.WriteLine("🔥 Starting MIPRO (Bayesian) Optimization...");

            // 4. Run Optimization
            var optimizedProgram = await mipro.CompileAsync(student, trainset);

            // 5. Assertions
            var newInstruction = optimizedProgram.State.Instruction;
            var demosCount = optimizedProgram.State.Demos.Count;

            _output.WriteLine("\n✅ Optimization Finished!");
            _output.WriteLine($"Optimized Instruction: {newInstruction}");
            _output.WriteLine($"Selected Demos Count: {demosCount}");

            Assert.NotNull(newInstruction);
            Assert.True(newInstruction.Length > 0);

            // 6. Test Run
            var testNews = "SpaceX успешно запустила Starship в пятый раз. Ракета вернулась на стартовую площадку и была поймана механическими руками 'Мехазиллы'.";
            var result = await optimizedProgram.InvokeAsync(new { Source = testNews });
            var pred = (Prediction)result;

            _output.WriteLine("\n=== Final Result ===");
            _output.WriteLine(pred.Get<string>("Post"));
            
            Assert.Contains("SpaceX", pred.Get<string>("Post"));
        }
    }
}