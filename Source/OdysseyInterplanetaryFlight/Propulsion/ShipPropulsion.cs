using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class ShipPropulsionReport
        {
            public float shipMass;
            public float thrust;
            public float totalFuel;
            public float fuelNeeded;
            public float maxRange;
            public float travelDays;
            public float accelerationFactor;
            public bool hasEnoughFuel;
            public bool hasEnoughThrust;
            public string blockingReason;

            public float MassToThrustRatio
            {
                get
                {
                    if (thrust <= 0.001f)
                        return 9999f;

                    return shipMass / thrust;
                }
            }

            public string SummaryLine
            {
                get
                {
                    return "Масса: " + shipMass.ToString("0.#") +
                           " | Тяга: " + thrust.ToString("0.#") +
                           " | Топливо: " + totalFuel.ToString("0.#") +
                           " | Дальность: " + maxRange.ToString("0.#");
                }
            }
        }

        public static class ShipPropulsionUtility
        {
            private const float BaseFuelPerDistance = 0.55f;
            private const float BaseMassDivisor = 140f;
            private const float BaseThrustDivisor = 65f;
            private const float MinFuelCost = 2f;
            private const float MinThrustForLaunch = 8f;
            private const float MaxMassToThrustRatio = 18f;
            private const float DefaultPawnMass = 35f;
            private const float DefaultItemMass = 1f;

            public static ShipPropulsionReport Evaluate(Thing shipAnchor, OrbitalNode source, OrbitalNode destination)
            {
                if (!ShipCaptureUtility.TryCollectShipCluster(shipAnchor, out ShipClusterData cluster))
                {
                    return new ShipPropulsionReport
                    {
                        blockingReason = "Не удалось определить кластер корабля.",
                        hasEnoughFuel = false,
                        hasEnoughThrust = false
                    };
                }

                return Evaluate(cluster, source, destination);
            }

            public static ShipPropulsionReport Evaluate(ShipClusterData cluster, OrbitalNode source, OrbitalNode destination)
            {
                ShipPropulsionReport report = new ShipPropulsionReport();
                if (cluster == null)
                {
                    report.blockingReason = "Нет данных о корабле.";
                    return report;
                }

                report.shipMass = CalculateShipMass(cluster);
                report.thrust = CalculateTotalThrust(cluster);
                report.totalFuel = CalculateTotalFuel(cluster);

                float distance = Mathf.Max(0f, OrbitalMath.Distance(source, destination));
                report.accelerationFactor = Mathf.Clamp01(report.thrust / Mathf.Max(1f, report.shipMass * 0.22f));
                float speedFactor = Mathf.Max(0.18f, report.thrust / Mathf.Max(25f, report.shipMass));
                report.travelDays = Mathf.Max(0.2f, distance / Mathf.Max(10f, 45f * speedFactor));

                float massFactor = Mathf.Max(0.35f, report.shipMass / BaseMassDivisor);
                float thrustFactor = Mathf.Max(0.35f, report.thrust / BaseThrustDivisor);

                report.fuelNeeded = Mathf.Max(MinFuelCost, distance * BaseFuelPerDistance * massFactor / thrustFactor);
                report.maxRange = report.totalFuel <= 0f
                    ? 0f
                    : (report.totalFuel * thrustFactor) / (BaseFuelPerDistance * Mathf.Max(0.35f, massFactor));

                report.hasEnoughThrust = report.thrust >= MinThrustForLaunch && report.MassToThrustRatio <= MaxMassToThrustRatio;
                report.hasEnoughFuel = report.totalFuel + 0.001f >= report.fuelNeeded;

                if (!report.hasEnoughThrust)
                    report.blockingReason = "Недостаточная тяга для массы корабля.";

                if (report.hasEnoughThrust && !report.hasEnoughFuel)
                    report.blockingReason = "Недостаточно топлива для выбранного маршрута.";

                return report;
            }

            public static void ConsumeFuel(ShipClusterData cluster, float fuelCost)
            {
                if (cluster == null || fuelCost <= 0.001f)
                    return;

                List<CompRefuelable> tanks = new List<CompRefuelable>();

                for (int i = 0; i < cluster.structuralThings.Count; i++)
                {
                    Thing thing = cluster.structuralThings[i];
                    if (thing == null)
                        continue;

                    CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
                    if (refuelable == null)
                        continue;

                    if (IsFuelBearingPart(thing))
                        tanks.Add(refuelable);
                }

                float remaining = fuelCost;

                for (int i = 0; i < tanks.Count && remaining > 0.001f; i++)
                {
                    CompRefuelable tank = tanks[i];
                    if (tank == null || tank.Fuel <= 0f)
                        continue;

                    float take = Mathf.Min(tank.Fuel, remaining);
                    tank.ConsumeFuel(take);
                    remaining -= take;
                }
            }

            private static float CalculateShipMass(ShipClusterData cluster)
            {
                float total = 0f;

                foreach (Thing thing in cluster.structuralThings)
                    total += GetThingMass(thing);

                foreach (Thing thing in cluster.items)
                    total += GetThingMass(thing);

                foreach (Pawn pawn in cluster.pawns)
                    total += GetPawnMass(pawn);

                return Mathf.Max(1f, total);
            }

            private static float CalculateTotalThrust(ShipClusterData cluster)
            {
                float total = 0f;

                foreach (Thing thing in cluster.structuralThings)
                {
                    if (thing == null || !IsEnginePart(thing))
                        continue;

                    total += GetEngineThrust(thing);
                }

                return total;
            }

            private static float CalculateTotalFuel(ShipClusterData cluster)
            {
                float total = 0f;

                foreach (Thing thing in cluster.structuralThings)
                {
                    if (thing == null)
                        continue;

                    CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
                    if (refuelable == null)
                        continue;

                    if (IsFuelBearingPart(thing))
                        total += refuelable.Fuel;
                }

                return total;
            }

            private static float GetThingMass(Thing thing)
            {
                if (thing == null || thing.def == null)
                    return 0f;

                try
                {
                    if (thing.def.statBases != null && thing.def.statBases.Any(s => s.stat == StatDefOf.Mass))
                        return Mathf.Max(0f, thing.GetStatValue(StatDefOf.Mass, true) * Mathf.Max(1, thing.stackCount));
                }
                catch
                {
                }

                try
                {
                    if (thing.def.BaseMass > 0f)
                        return Mathf.Max(0f, thing.def.BaseMass * Mathf.Max(1, thing.stackCount));
                }
                catch
                {
                }

                return DefaultItemMass * Mathf.Max(1, thing.stackCount);
            }

            private static float GetPawnMass(Pawn pawn)
            {
                if (pawn == null || pawn.Destroyed)
                    return 0f;

                try
                {
                    if (pawn.def != null && pawn.def.statBases != null && pawn.def.statBases.Any(s => s.stat == StatDefOf.Mass))
                        return Mathf.Max(1f, pawn.GetStatValue(StatDefOf.Mass, true));
                }
                catch
                {
                }

                return DefaultPawnMass;
            }

            private static float GetEngineThrust(Thing thing)
            {
                string defName = (thing.def?.defName ?? string.Empty).ToLowerInvariant();
                string label = (thing.def?.label ?? string.Empty).ToLowerInvariant();

                float thrust = 0f;

                if (defName.Contains("smallthruster") || label.Contains("small thruster"))
                    thrust = 32f;
                else if (defName.Contains("largethruster") || label.Contains("large thruster"))
                    thrust = 72f;
                else if (defName.Contains("thruster") || label.Contains("thruster"))
                    thrust = 45f;
                else if (defName.Contains("gravengine") || label.Contains("grav engine"))
                    thrust = 85f;
                else if (defName.Contains("engine") || label.Contains("engine"))
                    thrust = 50f;
                else
                    thrust = 35f;

                CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
                if (power != null && !power.PowerOn)
                    thrust *= 0.15f;

                if (thing.HitPoints > 0 && thing.MaxHitPoints > 0)
                    thrust *= Mathf.Clamp01((float)thing.HitPoints / thing.MaxHitPoints);

                return thrust;
            }

            private static bool IsEnginePart(Thing thing)
            {
                string defName = (thing?.def?.defName ?? string.Empty).ToLowerInvariant();
                return defName.Contains("gravengine") || defName.Contains("thruster") || defName.Contains("engine");
            }

            private static bool IsFuelBearingPart(Thing thing)
            {
                string defName = (thing?.def?.defName ?? string.Empty).ToLowerInvariant();
                return defName.Contains("chemfueltank") || defName.Contains("fueltank") || defName.Contains("tank") || IsEnginePart(thing) || defName.Contains("gravcore");
            }
        }
}
