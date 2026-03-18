using Verse;

namespace InterstellarOdyssey
{
    public class ShipLocationRecord : IExposable
    {
        public int shipThingId;
        public string shipDefName;
        public string shipLabel;
        public string currentNodeId = "homeworld";

        public void ExposeData()
        {
            Scribe_Values.Look(ref shipThingId, "shipThingId", 0);
            Scribe_Values.Look(ref shipDefName, "shipDefName");
            Scribe_Values.Look(ref shipLabel, "shipLabel");
            Scribe_Values.Look(ref currentNodeId, "currentNodeId", "homeworld");
        }
    }
}
