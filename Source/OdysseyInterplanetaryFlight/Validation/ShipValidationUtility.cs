using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public static class ShipValidationUtility
        {
            private const int MinimumCrew = 1;
            private const float MinimumTotalFuel = 10f;

            public static ShipValidationReport ValidateForLaunch(Thing shipAnchor)
            {
                ShipValidationReport report = new ShipValidationReport();

                if (!ShipCaptureUtility.TryCollectShipCluster(shipAnchor, out ShipClusterData cluster))
                {
                    report.AddCheck("Кластер корабля определён", false, "не удалось выделить корабль");
                    report.Error("Не удалось определить границы корабля.");
                    return report;
                }

                report.AddCheck("Кластер корабля определён", true, "клеток палубы: " + cluster.terrainCells.Count);

                ValidateRequiredSystems(cluster, report);
                ValidatePower(cluster, report);
                ValidateCrew(cluster, report);
                ValidateFuel(cluster, report);
                ValidateHullIntegrity(cluster, report);
                ValidateForeignPawns(cluster, report);
                ValidateForbiddenObjects(cluster, report);

                return report;
            }

            private static void ValidateRequiredSystems(ShipClusterData cluster, ShipValidationReport report)
            {
                bool hasCore = HasCore(cluster);
                bool hasEngine = HasEngine(cluster);
                bool hasTerminal = HasNavigationConsole(cluster);
                bool hasHull = cluster.terrainCells != null && cluster.terrainCells.Count > 0;

                report.AddCheck("Есть core", hasCore, hasCore ? null : "core не найден");
                report.AddCheck("Есть двигатель", hasEngine, hasEngine ? null : "двигатель не найден");
                report.AddCheck("Есть терминал", hasTerminal, hasTerminal ? null : "навигационный терминал не найден");
                report.AddCheck("Есть корпус/палуба", hasHull, hasHull ? (cluster.terrainCells.Count + " клеток") : "корабельный пол не найден");

                if (!hasCore)
                    report.Error("Не найден core корабля.");

                if (!hasEngine)
                    report.Error("Не найден двигатель корабля.");

                if (!hasTerminal)
                    report.Error("Не найден навигационный терминал.");

                if (!hasHull)
                    report.Error("Не найден корабельный корпус/палуба.");
            }

            private static void ValidatePower(ShipClusterData cluster, ShipValidationReport report)
            {
                Thing terminal = cluster.structuralThings.FirstOrDefault(IsNavigationConsole);
                if (terminal == null)
                {
                    report.AddCheck("Есть питание", false, "терминал не найден");
                    report.Error("Терминал не найден.");
                    return;
                }

                CompPowerTrader terminalPower = terminal.TryGetComp<CompPowerTrader>();
                bool terminalPowered = terminalPower == null || terminalPower.PowerOn;

                bool hasAnyPoweredCritical = cluster.structuralThings.Any(t =>
                {
                    if (t == null)
                        return false;

                    if (!IsCore(t) && !IsEngine(t) && !IsNavigationConsole(t))
                        return false;

                    CompPowerTrader comp = t.TryGetComp<CompPowerTrader>();
                    return comp == null || comp.PowerOn;
                });

                bool powerOk = terminalPowered && hasAnyPoweredCritical;
                string powerDetails = powerOk
                    ? "критические системы активны"
                    : (!terminalPowered ? "терминал без питания" : "критические системы не запитаны");

                report.AddCheck("Есть питание", powerOk, powerDetails);

                if (!terminalPowered)
                    report.Error("Навигационный терминал не запитан.");

                if (!hasAnyPoweredCritical)
                    report.Error("Критические системы корабля не запитаны.");
            }

            private static void ValidateCrew(ShipClusterData cluster, ShipValidationReport report)
            {
                int crewCount = cluster.pawns.Count(p =>
                    p != null &&
                    !p.Destroyed &&
                    !p.Dead &&
                    !p.Downed &&
                    p.Faction == Faction.OfPlayer &&
                    p.RaceProps != null &&
                    p.RaceProps.Humanlike);

                bool ok = crewCount >= MinimumCrew;
                report.AddCheck("Есть минимальный экипаж", ok,
                    ok ? ("экипаж: " + crewCount) : ("нужно минимум " + MinimumCrew + ", сейчас " + crewCount));

                if (!ok)
                    report.Error("Недостаточно экипажа. Минимум: " + MinimumCrew + ".");
            }

            private static void ValidateFuel(ShipClusterData cluster, ShipValidationReport report)
            {
                ShipPropulsionReport propulsion = ShipPropulsionUtility.Evaluate(cluster, null, null);

                bool hasFuel = propulsion.totalFuel >= MinimumTotalFuel;
                bool hasMass = propulsion.shipMass > 0.1f;
                bool hasThrust = propulsion.thrust > 0.001f && propulsion.hasEnoughThrust;
                bool hasRange = propulsion.maxRange > 0.1f;

                report.AddCheck("Есть запас топлива", hasFuel, "доступно: " + propulsion.totalFuel.ToString("0.#"));
                report.AddCheck("Масса корабля рассчитана", hasMass, "масса: " + propulsion.shipMass.ToString("0.#"));
                report.AddCheck("Достаточная тяга", hasThrust,
                    "тяга: " + propulsion.thrust.ToString("0.#") + ", отношение масса/тяга: " + propulsion.MassToThrustRatio.ToString("0.##"));
                report.AddCheck("Максимальная дальность", hasRange, "дальность: " + propulsion.maxRange.ToString("0.#"));

                if (!hasFuel)
                    report.Error("Недостаточный запас топлива. Нужно минимум " + MinimumTotalFuel.ToString("0.#") + ".");

                if (!hasMass)
                    report.Error("Не удалось рассчитать массу корабля.");

                if (!hasThrust)
                    report.Error("Недостаточная тяга для массы корабля.");

                if (!hasRange)
                    report.Warning("Не удалось определить максимальную дальность корабля.");
            }

            private static void ValidateHullIntegrity(ShipClusterData cluster, ShipValidationReport report)
            {
                if (cluster.map == null || cluster.terrainCells == null || cluster.terrainCells.Count == 0)
                {
                    report.AddCheck("Корпус герметичен", false, "не удалось проверить корпус");
                    return;
                }

                List<IntVec3> unroofed = new List<IntVec3>();
                List<IntVec3> breaches = new List<IntVec3>();

                foreach (IntVec3 cell in cluster.terrainCells)
                {
                    if (!cell.InBounds(cluster.map))
                        continue;

                    RoofDef roof = cluster.map.roofGrid.RoofAt(cell);
                    if (roof == null)
                        unroofed.Add(cell);

                    for (int i = 0; i < 4; i++)
                    {
                        IntVec3 next = cell + GenAdj.CardinalDirections[i];
                        if (!next.InBounds(cluster.map))
                            continue;

                        if (cluster.terrainCells.Contains(next))
                            continue;

                        if (!HasHullBoundaryAt(cluster.map, cell))
                        {
                            breaches.Add(cell);
                            break;
                        }
                    }
                }

                bool sealedHull = unroofed.Count == 0 && breaches.Count == 0;
                string hullDetails = sealedHull
                    ? "утечек не найдено"
                    : ("без крыши: " + unroofed.Count + ", пробоин: " + breaches.Count);

                report.AddCheck("Корпус герметичен", sealedHull, hullDetails);

                if (unroofed.Count > 0)
                    report.Error("Корпус негерметичен: есть непокрытые крышей клетки (" + unroofed.Count + ").");

                if (breaches.Count > 0)
                    report.Error("Корпус негерметичен: обнаружены пробоины/открытые участки периметра (" + breaches.Count + ").");
            }

            private static void ValidateForeignPawns(ShipClusterData cluster, ShipValidationReport report)
            {
                List<Pawn> foreign = cluster.pawns.Where(p =>
                    p != null &&
                    !p.Destroyed &&
                    !p.Dead &&
                    p.Faction != Faction.OfPlayer &&
                    !p.IsPrisonerOfColony).ToList();

                bool ok = foreign.Count == 0;
                string details = ok
                    ? "посторонних нет"
                    : string.Join(", ", foreign.Take(5).Select(p => p.LabelShortCap)) + (foreign.Count > 5 ? " ..." : string.Empty);

                report.AddCheck("Нет чужих пешек/животных", ok, details);

                if (!ok)
                    report.Error("На борту есть посторонние: " + details + ".");
            }

            private static void ValidateForbiddenObjects(ShipClusterData cluster, ShipValidationReport report)
            {
                List<Thing> forbidden = cluster.allBuildings.Where(IsForbiddenObjectForLaunch).ToList();

                bool ok = forbidden.Count == 0;
                string details = ok
                    ? "недопустимых построек нет"
                    : string.Join(", ", forbidden.Take(5).Select(t => t.LabelCap)) + (forbidden.Count > 5 ? " ..." : string.Empty);

                report.AddCheck("Нет запрещённых построек", ok, details);

                if (!ok)
                    report.Error("На корабле есть запрещённые/недопустимые объекты: " + details + ".");
            }

            private static bool HasCore(ShipClusterData cluster)
            {
                return cluster != null && cluster.structuralThings.Any(IsCore);
            }

            private static bool HasEngine(ShipClusterData cluster)
            {
                return cluster != null && cluster.structuralThings.Any(IsEngine);
            }

            private static bool HasNavigationConsole(ShipClusterData cluster)
            {
                return cluster != null && cluster.structuralThings.Any(IsNavigationConsole);
            }

            private static bool IsCore(Thing thing)
            {
                string defName = thing?.def?.defName ?? string.Empty;
                string lower = defName.ToLowerInvariant();
                return lower.Contains("gravcore") || lower.Contains("shipcore") || lower.Contains("core");
            }

            private static bool IsEngine(Thing thing)
            {
                string defName = thing?.def?.defName ?? string.Empty;
                string lower = defName.ToLowerInvariant();
                return lower.Contains("gravengine") || lower.Contains("thruster") || lower.Contains("engine");
            }

            private static bool IsNavigationConsole(Thing thing)
            {
                string defName = thing?.def?.defName ?? string.Empty;
                string lower = defName.ToLowerInvariant();
                return lower.Contains("navigationconsole") || lower.Contains("pilotconsole") || lower.Contains("shipnavigationconsole");
            }

            private static bool HasHullBoundaryAt(Map map, IntVec3 cell)
            {
                if (map == null || !cell.InBounds(map))
                    return false;

                List<Thing> things = map.thingGrid.ThingsListAt(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing == null || thing.Destroyed || thing.def == null)
                        continue;

                    if (thing.def.category != ThingCategory.Building)
                        continue;

                    if (thing.def.IsDoor)
                        return true;

                    if (thing.def.passability == Traversability.Impassable)
                        return true;

                    string defName = (thing.def.defName ?? string.Empty).ToLowerInvariant();
                    if (defName.Contains("hull") || defName.Contains("wall"))
                        return true;
                }

                return false;
            }

            private static bool IsForbiddenObjectForLaunch(Thing thing)
            {
                if (thing == null || thing.def == null)
                    return false;

                if (thing is Blueprint || thing is Frame)
                    return true;

                if (thing.def.category == ThingCategory.Building)
                {
                    if (thing.Faction != null && thing.Faction != Faction.OfPlayer)
                        return true;
                }

                return false;
            }
        }
}
