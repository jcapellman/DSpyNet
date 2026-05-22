// DSpyNet.Examples/Showcases/FacilitySupportData.cs
using System.Collections.Generic;
using DSpyNet.DSPy.Core;

namespace DSpyNet.Examples.Showcases
{
    /// <summary>
    /// Synthetic facility-support emails using the canonical DSPy GEPA tutorial schema.
    /// Full 200-example benchmark:
    ///   tutorial: https://dspy.ai/tutorials/gepa_facilitysupportanalyzer/
    ///   raw data: https://raw.githubusercontent.com/meta-llama/llama-prompt-ops/main/use-cases/facility-support-analyzer/dataset.json
    /// </summary>
    public static class FacilitySupportData
    {
        public static List<Example> Build() => new()
        {
            Example.From(("Message", "AC unit on floor 3 stopped working, conference room is 90F, meeting in 1 hour"), ("Urgency", "high"), ("Sentiment", "negative"), ("Categories", "emergency_repair_services,facility_management_issues")),
            Example.From(("Message", "Could we adjust the bi-weekly cleaning to Mondays instead of Wednesdays?"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "cleaning_services_scheduling")),
            Example.From(("Message", "Broken window in lobby, glass on the floor, safety hazard"), ("Urgency", "high"), ("Sentiment", "negative"), ("Categories", "emergency_repair_services,quality_and_safety_concerns")),
            Example.From(("Message", "Lightbulb out in stairwell B"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "routine_maintenance_requests")),
            Example.From(("Message", "Elevator stuck between floors 2 and 3, person trapped inside"), ("Urgency", "high"), ("Sentiment", "negative"), ("Categories", "emergency_repair_services,quality_and_safety_concerns")),
            Example.From(("Message", "Quarterly HVAC inspection due next month, please schedule"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "routine_maintenance_requests")),
            Example.From(("Message", "Restroom on floor 4 has been leaking for a few days, getting worse"), ("Urgency", "medium"), ("Sentiment", "negative"), ("Categories", "facility_management_issues,routine_maintenance_requests")),
            Example.From(("Message", "Thanks for fixing the boiler so quickly last week, the team really appreciated it"), ("Urgency", "low"), ("Sentiment", "positive"), ("Categories", "customer_feedback_and_complaints")),
            Example.From(("Message", "Replace ceiling tiles in the east wing, minor staining, not urgent"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "routine_maintenance_requests")),
            Example.From(("Message", "Fire alarm system test failed during audit, needs immediate attention"), ("Urgency", "high"), ("Sentiment", "negative"), ("Categories", "emergency_repair_services,quality_and_safety_concerns")),
            Example.From(("Message", "Need post-construction deep clean for the renovated 5th floor before tenants move in next week"), ("Urgency", "medium"), ("Sentiment", "neutral"), ("Categories", "specialized_cleaning_services,cleaning_services_scheduling")),
            Example.From(("Message", "Can your team run a training session for our staff on the new chemical handling procedures?"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "training_and_support_requests")),
            Example.From(("Message", "Inquiring about your eco-friendly cleaning options and waste reduction practices"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "sustainability_and_environmental_practices,general_inquiries")),
            Example.From(("Message", "Disappointed with last week's service quality, several areas were missed"), ("Urgency", "medium"), ("Sentiment", "negative"), ("Categories", "customer_feedback_and_complaints,quality_and_safety_concerns")),
            Example.From(("Message", "What service plans do you offer for a 12-story office building?"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "general_inquiries")),
            Example.From(("Message", "Power outage affecting servers in the IT room, backup batteries kicking in"), ("Urgency", "high"), ("Sentiment", "negative"), ("Categories", "emergency_repair_services,facility_management_issues")),
            Example.From(("Message", "Strong gas smell in the basement maintenance area, evacuating staff now"), ("Urgency", "high"), ("Sentiment", "negative"), ("Categories", "emergency_repair_services,quality_and_safety_concerns")),
            Example.From(("Message", "Water main burst is flooding the parking garage, vehicles at risk"), ("Urgency", "high"), ("Sentiment", "negative"), ("Categories", "emergency_repair_services,facility_management_issues")),
            Example.From(("Message", "Loose railing on 6th floor balcony, immediate fall risk for anyone leaning on it"), ("Urgency", "high"), ("Sentiment", "negative"), ("Categories", "quality_and_safety_concerns,routine_maintenance_requests")),
            Example.From(("Message", "Several office chairs in the bullpen are squeaky and uncomfortable"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "routine_maintenance_requests")),
            Example.From(("Message", "Vending machine on 2nd floor is malfunctioning, taking money without dispensing"), ("Urgency", "low"), ("Sentiment", "negative"), ("Categories", "facility_management_issues")),
            Example.From(("Message", "Lobby decor is looking dated, would like to discuss a refresh"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "general_inquiries")),
            Example.From(("Message", "Heating in the north wing has been inconsistent for two weeks"), ("Urgency", "medium"), ("Sentiment", "negative"), ("Categories", "facility_management_issues,routine_maintenance_requests")),
            Example.From(("Message", "Some staff are confused about the new building access process, can you clarify the steps?"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "training_and_support_requests")),
            Example.From(("Message", "Carpet in conference room A is showing significant wear, time to plan a replacement"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "routine_maintenance_requests")),
            Example.From(("Message", "We need additional recycling bins in the courtyard for our sustainability initiative"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "sustainability_and_environmental_practices")),
            Example.From(("Message", "Coffee was spilled across the carpet during last night's event, needs specialized treatment"), ("Urgency", "medium"), ("Sentiment", "neutral"), ("Categories", "specialized_cleaning_services")),
            Example.From(("Message", "Looking for a window-cleaning quote for our 14-story high-rise, exterior only"), ("Urgency", "medium"), ("Sentiment", "neutral"), ("Categories", "specialized_cleaning_services,cleaning_services_scheduling")),
            Example.From(("Message", "Wanted to say thanks — your team's professionalism on the audit prep was outstanding"), ("Urgency", "low"), ("Sentiment", "positive"), ("Categories", "customer_feedback_and_complaints")),
            Example.From(("Message", "Inquiring about your energy-saving practices and any LEED certifications you support"), ("Urgency", "low"), ("Sentiment", "neutral"), ("Categories", "sustainability_and_environmental_practices,general_inquiries"))
        };
    }
}
