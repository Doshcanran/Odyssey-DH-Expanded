using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public enum InterstellarTransitStage
        {
            None,
            InTransit,
            AwaitingLanding
        }

        public enum OrbitalNodeType
        {
            Planet,
            Station,
            Asteroid,
            AsteroidBelt
        }

        public enum ShipLandingMode
        {
            Precise,
            Emergency,
            OrbitalDrop,
            UnpreparedSurface,
            StationDocking
        }

        public enum TransitEventType
        {
            EngineBreakdown,
            EnergyLeak,
            SolarStorm,
            Drift,
            PirateSignal,
            DebrisDiscovery
        }
}
