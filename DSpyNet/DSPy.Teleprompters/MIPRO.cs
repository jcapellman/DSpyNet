// DSpyNet/DSPy.Teleprompters/MIPRO.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Evaluation;
using SharpLearning.Optimization; // Библиотека для Байесовской оптимизации

namespace DSpyNet.DSPy.Teleprompters
{
    public class MIPRO<TModule> : ITeleprompter<TModule> where TModule : Module
    {
        private readonly ILM _promptModel;
        private readonly Metric _metric;
        private readonly int _numCandidates; 
        private readonly int _numDemoSets;   
        private readonly int _numEvaluations; 
        private readonly int _maxBootstrappedDemos;
        private readonly ILogger _logger;
        private readonly Evaluator _evaluator;

        public MIPRO(
            ILM promptModel,
            Metric metric,
            int numCandidates = 5,
            int numDemoSets = 3,
            int numEvaluations = 20, 
            int maxBootstrappedDemos = 4,
            ILogger logger = null)
        {
            _promptModel = promptModel;
            _metric = metric;
            _numCandidates = numCandidates;
            _numDemoSets = numDemoSets;
            _numEvaluations = numEvaluations;
            _maxBootstrappedDemos = maxBootstrappedDemos;
            _logger = logger;
            _evaluator = new Evaluator(logger);
        }

        public async Task<TModule> CompileAsync(TModule student, List<Example> trainset, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Starting MIPRO (Bayesian) Optimization...");

            // 1. Generate Candidates (Instructions)
            var instructionCandidates = await GenerateInstructionsAsync(student, trainset, cancellationToken);
            instructionCandidates.Insert(0, GetCurrentInstruction(student)); // Add baseline

            // 2. Generate Demo Set Candidates
            var demoSetCandidates = GenerateDemoSets(student, trainset);
            demoSetCandidates.Insert(0, new List<Example>()); // Add zero-shot baseline

            _logger?.LogInformation($"Search Space: {instructionCandidates.Count} instructions x {demoSetCandidates.Count} demo sets.");

            // 3. Setup Bayesian Optimization
            var predictors = student.NamedPredictors(); 
            var parameters = new List<IParameterSpec>();

            // Для каждого предиктора создаем 2 параметра (индекс инструкции, индекс демо-сета)
            // Используем Transform.Linear, так как мы будем мапить double -> int индекс
            for (int i = 0; i < predictors.Count; i++)
            {
                // Диапазон индексов для инструкций: [0, Count - 1]
                parameters.Add(new MinMaxParameterSpec(0, instructionCandidates.Count - 1, Transform.Linear)); 
                // Диапазон индексов для демок: [0, Count - 1]
                parameters.Add(new MinMaxParameterSpec(0, demoSetCandidates.Count - 1, Transform.Linear));     
            }

            // Функция потерь (Objective Function), которую Байес будет минимизировать.
            // SharpLearning работает синхронно, поэтому внутри вызываем .GetAwaiter().GetResult().
            Func<double[], OptimizerResult> objective = (parameterValues) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try 
                {
                    // Клонируем модуль для эксперимента
                    var candidate = (TModule)student.DeepClone();
                    var candPredictors = candidate.NamedPredictors();

                    int paramIdx = 0;
                    for (int i = 0; i < candPredictors.Count; i++)
                    {
                        // PRODUCTION SAFEGUARD: 
                        // Оптимизатор может вернуть double (например, 2.999), нам нужен int индекс.
                        // Используем Round и Clamp, чтобы не выйти за границы массива.

                        double instrVal = parameterValues[paramIdx++];
                        double demoVal = parameterValues[paramIdx++];

                        int instrIdx = (int)Math.Round(instrVal);
                        int demoIdx = (int)Math.Round(demoVal);

                        instrIdx = Math.Clamp(instrIdx, 0, instructionCandidates.Count - 1);
                        demoIdx = Math.Clamp(demoIdx, 0, demoSetCandidates.Count - 1);

                        // Применяем параметры
                        var pred = candPredictors[i];
                        pred.State.Instruction = instructionCandidates[instrIdx];
                        pred.State.Demos = new List<Example>(demoSetCandidates[demoIdx]);
                    }

                    // Оценка
                    // Используем весь trainset. В продакшене тут лучше использовать Validation Set.
                    double score = _evaluator.EvaluateAsync(candidate, trainset, _metric, cancellationToken).GetAwaiter().GetResult();

                    // Конвертируем Score (чем больше, тем лучше) в Error (чем меньше, тем лучше)
                    // Предполагаем, что score в процентах (0-100)
                    double error = 100.0 - score;

                    _logger?.LogDebug($"[MIPRO Eval] Score: {score:F2}% (Error: {error:F2})");

                    return new OptimizerResult(parameterValues, error);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[MIPRO] Critical error during evaluation iteration.");
                    // Возвращаем максимальную ошибку, чтобы оптимизатор не шел в эту сторону
                    return new OptimizerResult(parameterValues, 1000.0); 
                }
            };

            // 4. Run Optimization
            _logger?.LogInformation($"Running Bayesian Optimization for {_numEvaluations} iterations...");

            // Исправлено: параметр seed и имя iterations
            var optimizer = new BayesianOptimizer(parameters: parameters.ToArray(), iterations: _numEvaluations, seed: 42);

            // Исправлено: Optimize возвращает массив результатов, берем лучший (с минимальной ошибкой)
            OptimizerResult[] results = optimizer.Optimize(objective);
            OptimizerResult bestResult = results.OrderBy(r => r.Error).First();

            _logger?.LogInformation($"MIPRO Finished. Best Error: {bestResult.Error:F2} (Score: {100.0 - bestResult.Error:F2}%)");

            // 5. Apply Best Parameters to Result Module
            var bestModule = (TModule)student.DeepClone();
            var bestPredictors = bestModule.NamedPredictors();

            int pIdx = 0;
            for (int i = 0; i < bestPredictors.Count; i++)
            {
                int instrIdx = (int)Math.Round(bestResult.ParameterSet[pIdx++]);
                int demoIdx = (int)Math.Round(bestResult.ParameterSet[pIdx++]);

                // Safety Clamp again
                instrIdx = Math.Clamp(instrIdx, 0, instructionCandidates.Count - 1);
                demoIdx = Math.Clamp(demoIdx, 0, demoSetCandidates.Count - 1);

                bestPredictors[i].State.Instruction = instructionCandidates[instrIdx];
                bestPredictors[i].State.Demos = new List<Example>(demoSetCandidates[demoIdx]);

                _logger?.LogInformation($"  Predictor {i}: Winner Instr #{instrIdx}, Demos #{demoIdx}");
            }

            return bestModule;
        }

        private async Task<List<string>> GenerateInstructionsAsync(TModule student, List<Example> trainset, CancellationToken cancellationToken)
        {
            var candidates = new List<string>();
            var generator = new Predict<GenerateInstructionSignature>(_promptModel);

            // Берем 3 примера для контекста мета-промпта
            var summaryExamples = trainset.Take(3).Select(ex => ex.ToString());
            var examplesStr = string.Join("\n", summaryExamples);
            var basicInstr = GetCurrentInstruction(student);

            for (int i = 0; i < _numCandidates; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try 
                {
                    // В реальном сценарии здесь нужно увеличивать Temperature у модели для разнообразия,
                    // если ILM поддерживает изменение параметров на лету.
                    var result = await generator.InvokeAsync(new { 
                        BasicInstruction = basicInstr,
                        Examples = examplesStr
                    }, cancellationToken) as Prediction;

                    if (result != null)
                    {
                        var proposed = result.Get<string>("ProposedInstruction");
                        // Очистка от кавычек и мусора, если модель выдала лишнее
                        proposed = proposed?.Trim().Trim('"').Trim('`');

                        if (!string.IsNullOrWhiteSpace(proposed))
                        {
                            candidates.Add(proposed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error generating instruction candidate");
                }
            }

            return candidates.Distinct().ToList();
        }

        private List<List<Example>> GenerateDemoSets(TModule student, List<Example> trainset)
        {
            var sets = new List<List<Example>>();
            var random = new Random();

            for (int i = 0; i < _numDemoSets; i++)
            {
                // Randomly sample N examples
                var subset = trainset.OrderBy(x => random.Next()).Take(_maxBootstrappedDemos).ToList();
                sets.Add(subset);
            }

            return sets;
        }

        private string GetCurrentInstruction(TModule module)
        {
            var predictors = module.NamedPredictors();
            if (predictors.Count > 0)
            {
                return predictors[0].State.Instruction;
            }
            return "Answer the question.";
        }
    }
}