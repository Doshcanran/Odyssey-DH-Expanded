using Verse;

namespace InterstellarOdyssey
{
    public class ShipTransitEvent : IExposable
    {
        public TransitEventType type;
        public int tick;
        public string title;
        public string description;
        public string impactSummary;
        public float severity;
        public bool changesRecommendedLandingMode;
        public ShipLandingMode recommendedLandingMode = ShipLandingMode.Precise;
        public int delayTicks;
        public int salvageSteel;
        public int salvageComponents;

        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type", TransitEventType.EngineBreakdown);
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref title, "title");
            Scribe_Values.Look(ref description, "description");
            Scribe_Values.Look(ref impactSummary, "impactSummary");
            Scribe_Values.Look(ref severity, "severity", 0f);
            Scribe_Values.Look(ref changesRecommendedLandingMode, "changesRecommendedLandingMode", false);
            Scribe_Values.Look(ref recommendedLandingMode, "recommendedLandingMode", ShipLandingMode.Precise);
            Scribe_Values.Look(ref delayTicks, "delayTicks", 0);
            Scribe_Values.Look(ref salvageSteel, "salvageSteel", 0);
            Scribe_Values.Look(ref salvageComponents, "salvageComponents", 0);
        }
    }
}
