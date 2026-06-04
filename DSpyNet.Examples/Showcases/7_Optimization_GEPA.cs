// DSpyNet.Examples/Showcases/7_Optimization_GEPA.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Evaluation;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using DSpyNet.Examples.Config;
using DSpyNet.Examples.Core;
using Microsoft.Extensions.Logging;

namespace DSpyNet.Examples.Showcases
{
    [DspInstruction("Read the support message and determine its urgency.")]
    public class UrgencySig : IDSpySignature
    {
        [DspInput(Prefix = "Message:")] public string Message { get; set; }
        [DspOutput(Prefix = "Urgency:", Description = "One of: low, medium, high")] public string Urgency { get; set; }
    }

    [DspInstruction("Read the support message and determine the sender's sentiment.")]
    public class SentimentSig : IDSpySignature
    {
        [DspInput(Prefix = "Message:")] public string Message { get; set; }
        [DspOutput(Prefix = "Sentiment:", Description = "One of: positive, neutral, negative")] public string Sentiment { get; set; }
    }

    [DspInstruction("Read the support message and list the facility-issue categories it mentions.")]
    public class CategoriesSig : IDSpySignature
    {
        [DspInput(Prefix = "Message:")] public string Message { get; set; }
        [DspOutput(Prefix = "Categories:", Description = "Comma-separated subset of: cleaning_services_scheduling, customer_feedback_and_complaints, emergency_repair_services, facility_management_issues, general_inquiries, quality_and_safety_concerns, routine_maintenance_requests, specialized_cleaning_services, sustainability_and_environmental_practices, training_and_support_requests")]
        public string Categories { get; set; }
    }

    public class FacilitySupportModule : Module
    {
        public Predict<UrgencySig> UrgencyPredict;
        public Predict<SentimentSig> SentimentPredict;
        public Predict<CategoriesSig> CategoriesPredict;

        public FacilitySupportModule(ILM lm, ILogger logger = null) : base(logger)
        {
            UrgencyPredict = new Predict<UrgencySig>(lm, logger);
            SentimentPredict = new Predict<SentimentSig>(lm, logger);
            CategoriesPredict = new Predict<CategoriesSig>(lm, logger);
        }

        public override async Task<object> InvokeAsync(object input, CancellationToken cancellationToken = default)
        {
            var u = (Prediction)await UrgencyPredict.InvokeAsync(input, cancellationToken);
            var s = (Prediction)await SentimentPredict.InvokeAsync(input, cancellationToken);
            var c = (Prediction)await CategoriesPredict.InvokeAsync(input, cancellationToken);
            return new Prediction(new Dictionary<string, object>
            {
                ["Urgency"] = u.Get<string>("Urgency") ?? "",
                ["Sentiment"] = s.Get<string>("Sentiment") ?? "",
                ["Categories"] = c.Get<string>("Categories") ?? ""
            });
        }

        // Composite modules must override DeepClone — base shallow-copies child predictors.
        public override Module DeepClone()
        {
            var clone = (FacilitySupportModule)MemberwiseClone();
            clone.UrgencyPredict = (Predict<UrgencySig>)UrgencyPredict.DeepClone();
            clone.SentimentPredict = (Predict<SentimentSig>)SentimentPredict.DeepClone();
            clone.CategoriesPredict = (Predict<CategoriesSig>)CategoriesPredict.DeepClone();
            return clone;
        }

        protected override void CopyStateFrom(Module source) { }
    }

    public class OptimizationGepaExample : ExampleRunner
    {
        public override string Name => "Optimization (GEPA)";
        public override string Description => "Reflective Prompt Evolution on a multi-predictor Facility Support Analyzer.";

        public OptimizationGepaExample(AppConfig config, ILoggerFactory loggerFactory) : base(config, loggerFactory) { }

        public override async Task RunAsync()
        {
            PrintHeader("Running GEPA: Facility Support Analyzer");
            PrintInfo("Classify each email into Urgency / Sentiment / Categories; GEPA mutates per-predictor prompts via reflection.");

            var trainset = FacilitySupportData.Build();
            var student = new FacilitySupportModule(_lm, _logger);
            var evaluator = new Evaluator(_logger);

            FeedbackMetric metric = (gold, pred, predName) =>
            {
                double sU = Eq(gold, pred, "Urgency") ? 1 : 0;
                double sS = Eq(gold, pred, "Sentiment") ? 1 : 0;
                double sC = CategoryF1(gold.Get<string>("Categories"), pred.Get<string>("Categories"));
                double total = (sU + sS + sC) / 3.0;
                string field = predName switch
                {
                    "UrgencyPredict" => "Urgency",
                    "SentimentPredict" => "Sentiment",
                    "CategoriesPredict" => "Categories",
                    _ => null
                };
                string fb = field == null
                    ? $"Overall {total:F2}"
                    : $"{field}: expected '{gold.Get<string>(field)}', got '{pred.Get<string>(field)}'";
                return new ScoreFeedback(total, fb);
            };

            var baseline = await evaluator.EvaluatePerExampleAsync(student, trainset, metric);
            PrintInfo($"Baseline avg score: {baseline.Average():F3}");

            var gepa = new GEPA<FacilitySupportModule>(_lm, metric,
                new GEPAOptions { Auto = GEPAAutoBudget.Light, Seed = 42 }, _logger);

            Console.WriteLine("\n[GEPA] Compiling...");
            var compiled = await gepa.CompileAsync(student, trainset);
            var after = await evaluator.EvaluatePerExampleAsync(compiled, trainset, metric);

            PrintOutput("Optimized score", $"{after.Average():F3} (baseline {baseline.Average():F3})");
            PrintOutput("Urgency instr", compiled.UrgencyPredict.State.Instruction);
            PrintOutput("Sentiment instr", compiled.SentimentPredict.State.Instruction);
            PrintOutput("Categories instr", compiled.CategoriesPredict.State.Instruction);
        }

        private static bool Eq(Example gold, Prediction pred, string key) =>
            string.Equals(gold.Get<string>(key), pred.Get<string>(key), StringComparison.OrdinalIgnoreCase);

        private static double CategoryF1(string gold, string pred)
        {
            var g = SplitSet(gold);
            var p = SplitSet(pred);
            if (g.Count == 0 && p.Count == 0) return 1.0;
            if (g.Count == 0 || p.Count == 0) return 0.0;
            int tp = g.Intersect(p).Count();
            double prec = (double)tp / p.Count, rec = (double)tp / g.Count;
            return prec + rec == 0 ? 0 : 2 * prec * rec / (prec + rec);
        }

        private static HashSet<string> SplitSet(string s) =>
            new HashSet<string>((s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                StringComparer.OrdinalIgnoreCase);
    }
}
