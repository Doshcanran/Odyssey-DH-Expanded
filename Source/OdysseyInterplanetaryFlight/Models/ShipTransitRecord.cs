using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class ShipTransitRecord : IExposable
        {
            public int shipThingId;
            public string shipLabel;
            public string shipDefName;
            public string sourceId;
            public string destinationId;
            public int departureTick;
            public int arrivalTick;
            public InterstellarTransitStage stage;
            public ShipSnapshot snapshot;
            public int nextEventTick;
            public List<ShipTransitEvent> eventLog = new List<ShipTransitEvent>();
            public ShipLandingMode preferredLandingMode = ShipLandingMode.Precise;
            public int salvageSteel;
            public int salvageComponents;
            public float travelDisruption;

            public float Progress
            {
                get
                {
                    if (stage != InterstellarTransitStage.InTransit || Find.TickManager == null)
                        return stage == InterstellarTransitStage.AwaitingLanding ? 1f : 0f;

                    int duration = Mathf.Max(1, arrivalTick - departureTick);
                    int elapsed = Find.TickManager.TicksGame - departureTick;
                    return Mathf.Clamp01((float)elapsed / duration);
                }
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref shipThingId, "shipThingId", 0);
                Scribe_Values.Look(ref shipLabel, "shipLabel");
                Scribe_Values.Look(ref shipDefName, "shipDefName");
                Scribe_Values.Look(ref sourceId, "sourceId");
                Scribe_Values.Look(ref destinationId, "destinationId");
                Scribe_Values.Look(ref departureTick, "departureTick", 0);
                Scribe_Values.Look(ref arrivalTick, "arrivalTick", 0);
                Scribe_Values.Look(ref stage, "stage", InterstellarTransitStage.None);
                Scribe_Deep.Look(ref snapshot, "snapshot");
                Scribe_Values.Look(ref nextEventTick, "nextEventTick", 0);
                Scribe_Collections.Look(ref eventLog, "eventLog", LookMode.Deep);
                Scribe_Values.Look(ref preferredLandingMode, "preferredLandingMode", ShipLandingMode.Precise);
                Scribe_Values.Look(ref salvageSteel, "salvageSteel", 0);
                Scribe_Values.Look(ref salvageComponents, "salvageComponents", 0);
                Scribe_Values.Look(ref travelDisruption, "travelDisruption", 0f);

                if (eventLog == null)
                    eventLog = new List<ShipTransitEvent>();
            }
        }
}
