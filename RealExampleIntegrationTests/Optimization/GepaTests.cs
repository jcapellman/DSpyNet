// RealExampleIntegrationTests/Optimization/GepaTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Evaluation;
using DSpyNet.DSPy.Modules;
using DSpyNet.DSPy.Teleprompters;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace RealExampleIntegrationTests.Optimization
{
    [DspInstruction("Read the support message and determine its urgency.")]
    public class GepaUrgencySig : IDSpySignature
    {
        [DspInput(Prefix = "Message:")] public string Message { get; set; }
        [DspOutput(Prefix = "Urgency:", Description = "One of: low, medium, high")] public string Urgency { get; set; }
    }

    [DspInstruction("Read the support message and determine the sender's sentiment.")]
    public class GepaSentimentSig : IDSpySignature
    {
        [DspInput(Prefix = "Message:")] public string Message { get; set; }
        [DspOutput(Prefix = "Sentiment:", Description = "One of: positive, neutral, negative")] public string Sentiment { get; set; }
    }

    [DspInstruction("Read the support message and list the facility-issue categories it mentions.")]
    public class GepaCategoriesSig : IDSpySignature
    {
        [DspInput(Prefix = "Message:")] public string Message { get; set; }
        [DspOutput(Prefix = "Categories:", Description = "Comma-separated subset of: cleaning_services_scheduling, customer_feedback_and_complaints, emergency_repair_services, facility_management_issues, general_inquiries, quality_and_safety_concerns, routine_maintenance_requests, specialized_cleaning_services, sustainability_and_environmental_practices, training_and_support_requests")]
        public string Categories { get; set; }
    }

    public class GepaFacilityModule : Module
    {
        public Predict<GepaUrgencySig> UrgencyPredict;
        public Predict<GepaSentimentSig> SentimentPredict;
        public Predict<GepaCategoriesSig> CategoriesPredict;

        public GepaFacilityModule(ILM lm, ILogger logger = null) : base(logger)
        {
            UrgencyPredict = new Predict<GepaUrgencySig>(lm, logger);
            SentimentPredict = new Predict<GepaSentimentSig>(lm, logger);
            CategoriesPredict = new Predict<GepaCategoriesSig>(lm, logger);
        }

        public override async Task<object> InvokeAsync(object input)
        {
            var u = (Prediction)await UrgencyPredict.InvokeAsync(input);
            var s = (Prediction)await SentimentPredict.InvokeAsync(input);
            var c = (Prediction)await CategoriesPredict.InvokeAsync(input);
            return new Prediction(new Dictionary<string, object>
            {
                ["Urgency"] = u.Get<string>("Urgency") ?? "",
                ["Sentiment"] = s.Get<string>("Sentiment") ?? "",
                ["Categories"] = c.Get<string>("Categories") ?? ""
            });
        }

        public override Module DeepClone()
        {
            var clone = (GepaFacilityModule)MemberwiseClone();
            clone.UrgencyPredict = (Predict<GepaUrgencySig>)UrgencyPredict.DeepClone();
            clone.SentimentPredict = (Predict<GepaSentimentSig>)SentimentPredict.DeepClone();
            clone.CategoriesPredict = (Predict<GepaCategoriesSig>)CategoriesPredict.DeepClone();
            return clone;
        }

        protected override void CopyStateFrom(Module source) { }
    }

    public class GepaTests : BaseIntegrationTest
    {
        public GepaTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Optimize_FacilitySupport_WithGEPA()
        {
            var lm = GetRouterAiLM();

            // 1. Dataset — synthetic, canonical schema from the DSPy GEPA tutorial.
            // Full 200-example benchmark:
            //   https://dspy.ai/tutorials/gepa_facilitysupportanalyzer/
            //   https://raw.githubusercontent.com/meta-llama/llama-prompt-ops/main/use-cases/facility-support-analyzer/dataset.json
            var trainset = new List<Example>
            {
                Example.From(("Message", "AC unit on floor 3 stopped working, conference room is 90F"),
                             ("Urgency", "high"), ("Sentiment", "negative"),
                             ("Categories", "emergency_repair_services,facility_management_issues")),
                Example.From(("Message", "Could we adjust the bi-weekly cleaning to Mondays?"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "cleaning_services_scheduling")),
                Example.From(("Message", "Broken window in lobby, glass on the floor"),
                             ("Urgency", "high"), ("Sentiment", "negative"),
                             ("Categories", "emergency_repair_services,quality_and_safety_concerns")),
                Example.From(("Message", "Lightbulb out in stairwell B"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "routine_maintenance_requests")),
                Example.From(("Message", "Elevator stuck between floors 2 and 3, person trapped"),
                             ("Urgency", "high"), ("Sentiment", "negative"),
                             ("Categories", "emergency_repair_services,quality_and_safety_concerns")),
                Example.From(("Message", "Quarterly HVAC inspection due next month"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "routine_maintenance_requests")),
                Example.From(("Message", "Restroom on floor 4 has been leaking for a few days, getting worse"),
                             ("Urgency", "medium"), ("Sentiment", "negative"),
                             ("Categories", "facility_management_issues,routine_maintenance_requests")),
                Example.From(("Message", "Thanks for fixing the boiler so quickly last week"),
                             ("Urgency", "low"), ("Sentiment", "positive"),
                             ("Categories", "customer_feedback_and_complaints")),
                Example.From(("Message", "Replace ceiling tiles in the east wing, minor staining"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "routine_maintenance_requests")),
                Example.From(("Message", "Fire alarm system test failed during audit, needs immediate attention"),
                             ("Urgency", "high"), ("Sentiment", "negative"),
                             ("Categories", "emergency_repair_services,quality_and_safety_concerns")),
                Example.From(("Message", "Need post-construction deep clean for the renovated 5th floor"),
                             ("Urgency", "medium"), ("Sentiment", "neutral"),
                             ("Categories", "specialized_cleaning_services,cleaning_services_scheduling")),
                Example.From(("Message", "Can your team run a training session on the new chemical handling procedures?"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "training_and_support_requests")),
                Example.From(("Message", "Inquiring about your eco-friendly cleaning options and waste reduction"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "sustainability_and_environmental_practices,general_inquiries")),
                Example.From(("Message", "Disappointed with last week's service quality, several areas were missed"),
                             ("Urgency", "medium"), ("Sentiment", "negative"),
                             ("Categories", "customer_feedback_and_complaints,quality_and_safety_concerns")),
                Example.From(("Message", "What service plans do you offer for a 12-story office building?"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "general_inquiries")),
                Example.From(("Message", "Power outage affecting servers in the IT room, backup batteries kicking in"),
                             ("Urgency", "high"), ("Sentiment", "negative"),
                             ("Categories", "emergency_repair_services,facility_management_issues")),
                Example.From(("Message", "Strong gas smell in the basement maintenance area, evacuating staff now"),
                             ("Urgency", "high"), ("Sentiment", "negative"),
                             ("Categories", "emergency_repair_services,quality_and_safety_concerns")),
                Example.From(("Message", "Water main burst is flooding the parking garage, vehicles at risk"),
                             ("Urgency", "high"), ("Sentiment", "negative"),
                             ("Categories", "emergency_repair_services,facility_management_issues")),
                Example.From(("Message", "Loose railing on 6th floor balcony, immediate fall risk"),
                             ("Urgency", "high"), ("Sentiment", "negative"),
                             ("Categories", "quality_and_safety_concerns,routine_maintenance_requests")),
                Example.From(("Message", "Several office chairs in the bullpen are squeaky and uncomfortable"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "routine_maintenance_requests")),
                Example.From(("Message", "Vending machine on 2nd floor is malfunctioning, taking money without dispensing"),
                             ("Urgency", "low"), ("Sentiment", "negative"),
                             ("Categories", "facility_management_issues")),
                Example.From(("Message", "Lobby decor is looking dated, would like to discuss a refresh"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "general_inquiries")),
                Example.From(("Message", "Heating in the north wing has been inconsistent for two weeks"),
                             ("Urgency", "medium"), ("Sentiment", "negative"),
                             ("Categories", "facility_management_issues,routine_maintenance_requests")),
                Example.From(("Message", "Some staff are confused about the new building access process"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "training_and_support_requests")),
                Example.From(("Message", "Carpet in conference room A is showing significant wear"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "routine_maintenance_requests")),
                Example.From(("Message", "We need additional recycling bins in the courtyard for our sustainability initiative"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "sustainability_and_environmental_practices")),
                Example.From(("Message", "Coffee was spilled across the carpet during last night's event, needs specialized treatment"),
                             ("Urgency", "medium"), ("Sentiment", "neutral"),
                             ("Categories", "specialized_cleaning_services")),
                Example.From(("Message", "Looking for a window-cleaning quote for our 14-story high-rise"),
                             ("Urgency", "medium"), ("Sentiment", "neutral"),
                             ("Categories", "specialized_cleaning_services,cleaning_services_scheduling")),
                Example.From(("Message", "Wanted to say thanks - your team's professionalism on the audit prep was outstanding"),
                             ("Urgency", "low"), ("Sentiment", "positive"),
                             ("Categories", "customer_feedback_and_complaints")),
                Example.From(("Message", "Inquiring about your energy-saving practices and LEED certifications"),
                             ("Urgency", "low"), ("Sentiment", "neutral"),
                             ("Categories", "sustainability_and_environmental_practices,general_inquiries"))
            };

            // 2. Metric: per-predictor feedback so the reflection LM can act on it
            FeedbackMetric metric = (gold, pred, predName) =>
            {
                string gU = gold.Get<string>("Urgency"), pU = pred.Get<string>("Urgency");
                string gS = gold.Get<string>("Sentiment"), pS = pred.Get<string>("Sentiment");
                string gC = gold.Get<string>("Categories"), pC = pred.Get<string>("Categories");

                double sU = string.Equals(gU, pU, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                double sS = string.Equals(gS, pS, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                double sC = CategoryF1(gC, pC);
                double total = (sU + sS + sC) / 3.0;

                string fb = predName switch
                {
                    "UrgencyPredict" => $"Expected urgency '{gU}', got '{pU}'.",
                    "SentimentPredict" => $"Expected sentiment '{gS}', got '{pS}'.",
                    "CategoriesPredict" => $"Expected categories '{gC}', got '{pC}'.",
                    _ => $"Overall {total:F2}"
                };
                return new ScoreFeedback(total, fb);
            };

            // 3. Student
            var student = new GepaFacilityModule(lm, _logger);

            // 4. Configure GEPA
            var gepa = new GEPA<GepaFacilityModule>(
                reflectionLM: lm,
                metric: metric,
                options: new GEPAOptions { Auto = GEPAAutoBudget.Light, Seed = 42 },
                logger: _logger);

            _output.WriteLine("Starting GEPA Optimization...");

            // 5. Score baseline, compile, score result
            var evaluator = new Evaluator(_logger);
            var baseline = await evaluator.EvaluatePerExampleAsync(student, trainset, metric);
            var compiled = await gepa.CompileAsync(student, trainset);
            var after = await evaluator.EvaluatePerExampleAsync(compiled, trainset, metric);

            // 6. Inspect results
            _output.WriteLine("\nOptimization Finished!");
            _output.WriteLine($"Baseline avg score:  {baseline.Average():F3}");
            _output.WriteLine($"Optimized avg score: {after.Average():F3}");
            _output.WriteLine($"Urgency instr:    {compiled.UrgencyPredict.State.Instruction}");
            _output.WriteLine($"Sentiment instr:  {compiled.SentimentPredict.State.Instruction}");
            _output.WriteLine($"Categories instr: {compiled.CategoriesPredict.State.Instruction}");

            // Allow real-LM noise; require the optimizer not to regress badly.
            Assert.True(after.Average() >= baseline.Average() - 0.1,
                $"Optimized score {after.Average():F3} fell more than 0.1 below baseline {baseline.Average():F3}");
        }

        private static double CategoryF1(string gold, string pred)
        {
            var g = SplitSet(gold);
            var p = SplitSet(pred);
            if (g.Count == 0 && p.Count == 0) return 1.0;
            if (g.Count == 0 || p.Count == 0) return 0.0;
            int tp = g.Intersect(p).Count();
            double prec = (double)tp / p.Count;
            double rec = (double)tp / g.Count;
            return prec + rec == 0 ? 0 : 2 * prec * rec / (prec + rec);
        }

        private static HashSet<string> SplitSet(string s) =>
            new HashSet<string>((s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                StringComparer.OrdinalIgnoreCase);
    }
}
