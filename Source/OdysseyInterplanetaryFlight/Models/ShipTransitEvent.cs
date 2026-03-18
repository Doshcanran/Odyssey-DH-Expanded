using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class ShipTransitEvent : IExposable
        {
            public TransitEventType type;
            public int tick;
            public string title;
            public string description;
            public float severity;

            public void ExposeData()
            {
                Scribe_Values.Look(ref type, "type", TransitEventType.EngineBreakdown);
                Scribe_Values.Look(ref tick, "tick", 0);
                Scribe_Values.Look(ref title, "title");
                Scribe_Values.Look(ref description, "description");
                Scribe_Values.Look(ref severity, "severity", 0f);
            }
        }
}
