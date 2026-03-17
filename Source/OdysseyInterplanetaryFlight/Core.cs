
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public class InterstellarOdysseyMod : Mod
    {
        public InterstellarOdysseyMod(ModContentPack content) : base(content)
        {
        }
    }

    public class Building_ShipNavigationConsole : Building
    {
        private CompPowerTrader cachedPowerComp;

        public CompPowerTrader PowerComp
        {
            get
            {
                if (cachedPowerComp == null)
                    cachedPowerComp = GetComp<CompPowerTrader>();
                return cachedPowerComp;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
                yield return gizmo;

            yield return new Command_Action
            {
                defaultLabel = "Орбитальная карта",
                defaultDesc = "Открыть карту перелёта и выбрать пункт назначения.",
                icon = BaseContent.BadTex,
                action = delegate
                {
                    if (PowerComp != null && !PowerComp.PowerOn)
                    {
                        Messages.Message("Терминал не запитан.", MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Thing ship = ShipResolver.FindBestAvailableShip(this);
                    if (ship == null)
                    {
                        Messages.Message("Не найден гравикорабль рядом с терминалом.", MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Find.WindowStack.Add(new Window_OrbitalMap(ship));
                }
            };
        }
    }

    public class PlaceWorker_ShipNavigationConsole : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!ShipFloorUtility.IsShipFloorCell(map, loc))
                return new AcceptanceReport("Терминал можно ставить только на палубу/надстройку корабля.");

            return AcceptanceReport.WasAccepted;
        }
    }

    public static class ShipResolver
    {
        public static Thing FindBestAvailableShip(Building console)
        {
            if (console == null || console.Map == null)
                return null;

            return ShipCaptureUtility.FindShipAnchorForConsole(console);
        }
    }

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

    public class OrbitalNode : IExposable
    {
        public string id;
        public string label;
        public OrbitalNodeType type;
        public float angle;
        public float radius;

        public OrbitalNode()
        {
        }

        public OrbitalNode(string id, string label, OrbitalNodeType type, float angle, float radius)
        {
            this.id = id;
            this.label = label;
            this.type = type;
            this.angle = angle;
            this.radius = radius;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref type, "type", OrbitalNodeType.Planet);
            Scribe_Values.Look(ref angle, "angle", 0f);
            Scribe_Values.Look(ref radius, "radius", 0f);
        }
    }

    public class ShipThingSnapshot : IExposable
    {
        public Thing thing;
        public IntVec3 offset = IntVec3.Zero;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref thing, "thing");
            Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);
        }
    }

    public class ShipPawnSnapshot : IExposable
    {
        public Pawn pawn;
        public IntVec3 offset = IntVec3.Zero;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);
        }
    }

    public class ShipTerrainSnapshot : IExposable
    {
        public IntVec3 offset = IntVec3.Zero;
        public TerrainDef terrainDef;

        public void ExposeData()
        {
            Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);
            Scribe_Defs.Look(ref terrainDef, "terrainDef");
        }
    }

    public class ShipRoofSnapshot : IExposable
    {
        public IntVec3 offset = IntVec3.Zero;
        public RoofDef roofDef;

        public void ExposeData()
        {
            Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);
            Scribe_Defs.Look(ref roofDef, "roofDef");
        }
    }

    public class ShipSnapshot : IExposable
    {
        public int shipThingId;
        public string shipDefName;
        public string currentNodeId;
        public IntVec3 anchorCell = IntVec3.Zero;
        public List<ShipThingSnapshot> buildings = new List<ShipThingSnapshot>();
        public List<ShipThingSnapshot> items = new List<ShipThingSnapshot>();
        public List<ShipPawnSnapshot> pawns = new List<ShipPawnSnapshot>();
        public List<ShipTerrainSnapshot> terrains = new List<ShipTerrainSnapshot>();
        public List<ShipRoofSnapshot> roofs = new List<ShipRoofSnapshot>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref shipThingId, "shipThingId", 0);
            Scribe_Values.Look(ref shipDefName, "shipDefName");
            Scribe_Values.Look(ref currentNodeId, "currentNodeId");
            Scribe_Values.Look(ref anchorCell, "anchorCell", IntVec3.Zero);
            Scribe_Collections.Look(ref buildings, "buildings", LookMode.Deep);
            Scribe_Collections.Look(ref items, "items", LookMode.Deep);
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Deep);
            Scribe_Collections.Look(ref terrains, "terrains", LookMode.Deep);
            Scribe_Collections.Look(ref roofs, "roofs", LookMode.Deep);

            if (buildings == null) buildings = new List<ShipThingSnapshot>();
            if (items == null) items = new List<ShipThingSnapshot>();
            if (pawns == null) pawns = new List<ShipPawnSnapshot>();
            if (terrains == null) terrains = new List<ShipTerrainSnapshot>();
            if (roofs == null) roofs = new List<ShipRoofSnapshot>();
        }
    }

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
        }
    }

    public static class OrbitalMath
    {
        public static Vector2 Position(OrbitalNode node)
        {
            if (node == null)
                return Vector2.zero;

            float radians = node.angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * node.radius;
        }

        public static float Distance(OrbitalNode a, OrbitalNode b)
        {
            return Vector2.Distance(Position(a), Position(b));
        }
    }

    public static class ShipFloorUtility
    {
        private static readonly HashSet<string> ShipTerrainDefNames = new HashSet<string>
        {
            "GravshipDeck",
            "GravshipFloor",
            "GravshipSuperstructure",
            "ShipDeck",
            "ShipFloor",
            "Substructure",
            "Substruscure"
        };

        public static bool IsShipFloor(TerrainDef terrain)
        {
            if (terrain == null)
                return false;

            if (ShipTerrainDefNames.Contains(terrain.defName))
                return true;

            string defName = (terrain.defName ?? string.Empty).ToLowerInvariant();
            string label = (terrain.label ?? string.Empty).ToLowerInvariant();

            return defName.Contains("gravship")
                || defName.Contains("shipfloor")
                || defName.Contains("shipdeck")
                || defName.Contains("superstructure")
                || defName.Contains("substructure")
                || defName.Contains("substruscure")
                || defName.Contains("deck")
                || label.Contains("gravship")
                || label.Contains("палуб")
                || label.Contains("кораб")
                || label.Contains("надстрой");
        }

        public static bool IsShipFloorCell(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
                return false;

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            return IsShipFloor(terrain);
        }

        public static bool IsRestoreableShipTerrain(TerrainDef terrain)
        {
            return IsShipFloor(terrain);
        }
    }



    public class ShipClusterData
    {
        public Map map;
        public Faction faction;
        public Thing anchor;
        public IntVec3 anchorCell = IntVec3.Zero;
        public HashSet<IntVec3> terrainCells = new HashSet<IntVec3>();
        public List<Thing> structuralThings = new List<Thing>();
        public HashSet<IntVec3> occupancyCells = new HashSet<IntVec3>();
        public List<Thing> allBuildings = new List<Thing>();
        public List<Pawn> pawns = new List<Pawn>();
        public List<Thing> items = new List<Thing>();
    }


    public class ShipValidationCheck
    {
        public string label;
        public bool passed;
        public bool warningOnly;
        public string details;

        public ShipValidationCheck()
        {
        }

        public ShipValidationCheck(string label, bool passed, string details = null, bool warningOnly = false)
        {
            this.label = label;
            this.passed = passed;
            this.details = details;
            this.warningOnly = warningOnly;
        }
    }


    public class ShipValidationReport
    {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public readonly List<ShipValidationCheck> checks = new List<ShipValidationCheck>();

        public bool CanLaunch => errors.Count == 0;

        public void AddCheck(string label, bool passed, string details = null, bool warningOnly = false)
        {
            checks.Add(new ShipValidationCheck(label, passed, details, warningOnly));
        }

        public void Error(string text)
        {
            if (!string.IsNullOrEmpty(text))
                errors.Add(text);
        }

        public void Warning(string text)
        {
            if (!string.IsNullOrEmpty(text))
                warnings.Add(text);
        }

        public string ToUserText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Предстартовая проверка корабля");

            if (checks.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Чеклист:");
                for (int i = 0; i < checks.Count; i++)
                {
                    ShipValidationCheck check = checks[i];
                    string marker = check.passed ? "[OK]" : (check.warningOnly ? "[!]" : "[X]");
                    sb.AppendLine(marker + " " + check.label + (string.IsNullOrEmpty(check.details) ? string.Empty : ": " + check.details));
                }
            }

            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Ошибки:");
                for (int i = 0; i < errors.Count; i++)
                    sb.AppendLine("• " + errors[i]);
            }

            if (warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Предупреждения:");
                for (int i = 0; i < warnings.Count; i++)
                    sb.AppendLine("• " + warnings[i]);
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Корабль готов к старту.");
            }

            return sb.ToString().TrimEnd();
        }
    }

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
            string powerDetails = powerOk ? "критические системы активны" : (!terminalPowered ? "терминал без питания" : "критические системы не запитаны");
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
            report.AddCheck("Есть минимальный экипаж", ok, ok ? ("экипаж: " + crewCount) : ("нужно минимум " + MinimumCrew + ", сейчас " + crewCount));

            if (!ok)
                report.Error("Недостаточно экипажа. Минимум: " + MinimumCrew + ".");
        }


        private static void ValidateFuel(ShipClusterData cluster, ShipValidationReport report)
        {
            float totalFuel = 0f;
            bool hasAnyFuelSystem = false;
            int emptyModules = 0;

            foreach (Thing thing in cluster.structuralThings)
            {
                if (thing == null)
                    continue;

                CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
                if (refuelable == null)
                    continue;

                if (IsEngine(thing) || IsCore(thing) || IsFuelTank(thing))
                {
                    hasAnyFuelSystem = true;
                    totalFuel += refuelable.Fuel;

                    if (refuelable.Fuel <= 0.01f)
                    {
                        emptyModules++;
                        report.Error("Пустой топливный модуль: " + thing.LabelCap + ".");
                    }
                }
            }

            bool fuelOk = hasAnyFuelSystem && totalFuel >= MinimumTotalFuel && emptyModules == 0;
            string fuelDetails = !hasAnyFuelSystem
                ? "топливная система не найдена"
                : ("топливо: " + totalFuel.ToString("0.#") + (emptyModules > 0 ? ", пустых модулей: " + emptyModules : string.Empty));

            report.AddCheck("Есть запас топлива", fuelOk, fuelDetails);

            if (!hasAnyFuelSystem)
            {
                report.Error("Не найдена топливная система корабля.");
                return;
            }

            if (totalFuel < MinimumTotalFuel)
                report.Error("Недостаточный запас топлива. Нужно минимум " + MinimumTotalFuel.ToString("0.#") + ".");
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
            string hullDetails = sealedHull ? "утечек не найдено" : ("без крыши: " + unroofed.Count + ", пробоин: " + breaches.Count);
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
            string details = ok ? "посторонних нет" : string.Join(", ", foreign.Take(5).Select(p => p.LabelShortCap)) + (foreign.Count > 5 ? " ..." : string.Empty);
            report.AddCheck("Нет чужих пешек/животных", ok, details);

            if (!ok)
                report.Error("На борту есть посторонние: " + details + ".");
        }


        private static void ValidateForbiddenObjects(ShipClusterData cluster, ShipValidationReport report)
        {
            List<Thing> forbidden = cluster.allBuildings.Where(IsForbiddenObjectForLaunch).ToList();

            bool ok = forbidden.Count == 0;
            string details = ok ? "недопустимых построек нет" : string.Join(", ", forbidden.Take(5).Select(t => t.LabelCap)) + (forbidden.Count > 5 ? " ..." : string.Empty);
            report.AddCheck("Нет запрещённых построек", ok, details);

            if (!ok)
                report.Error("На корабле есть запрещённые/недопустимые объекты: " + details + ".");
        }

        private static bool HasCore(ShipClusterData cluster)
        {
            return cluster.structuralThings.Any(IsCore);
        }

        private static bool HasEngine(ShipClusterData cluster)
        {
            return cluster.structuralThings.Any(IsEngine);
        }

        private static bool HasNavigationConsole(ShipClusterData cluster)
        {
            return cluster.structuralThings.Any(IsNavigationConsole);
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

        private static bool IsFuelTank(Thing thing)
        {
            string defName = thing?.def?.defName ?? string.Empty;
            string lower = defName.ToLowerInvariant();
            return lower.Contains("chemfueltank") || lower.Contains("fueltank") || lower.Contains("tank");
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

    public static class ShipCaptureUtility
    {
        private static readonly HashSet<string> StructuralDefs = new HashSet<string>(StringComparer.Ordinal)
        {
            "GravEngine",
            "GravshipHull",
            "PilotConsole",
            "SmallThruster",
            "ChemfuelTank",
            "GravcorePowerCell",
            "Door",
            "IO_ShipNavigationConsole"
        };

        private static string GetShipStructureDefName(Thing thing)
        {
            if (thing == null || thing.def == null)
                return null;

            if (thing is Blueprint || thing is Frame)
            {
                ThingDef buildDef = thing.def.entityDefToBuild as ThingDef;
                if (buildDef != null)
                    return buildDef.defName;
            }

            return thing.def.defName;
        }

        private static bool IsKnownShipStructureDef(string defName)
        {
            return !string.IsNullOrEmpty(defName) && StructuralDefs.Contains(defName);
        }

        public static Thing FindShipAnchorForConsole(Building console)
        {
            if (console == null || console.Map == null)
                return null;

            Map map = console.Map;
            HashSet<IntVec3> shipCells = CollectConnectedShipTerrain(map, console.Position);
            if (shipCells.Count == 0)
                return null;

            Thing best = null;
            int bestScore = int.MinValue;

            List<Thing> allThings = map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing == null || thing.Destroyed || !thing.Spawned || thing.def == null)
                    continue;

                if (thing.Faction != console.Faction)
                    continue;

                if (!IsStructuralShipThing(thing))
                    continue;

                if (!IsThingConnectedToShipCluster(thing, shipCells))
                    continue;

                int score = ScorePotentialAnchor(thing, console.Position);
                if (score > bestScore)
                {
                    best = thing;
                    bestScore = score;
                }
            }

            if (best != null)
                return best;

            if (console.Spawned && IsThingConnectedToShipCluster(console, shipCells))
                return console;

            return null;
        }

        public static bool TryCollectShipCluster(Thing shipAnchor, out ShipClusterData cluster)
        {
            cluster = null;

            if (shipAnchor == null || shipAnchor.Map == null || shipAnchor.def == null)
                return false;

            Map map = shipAnchor.Map;
            Faction faction = shipAnchor.Faction;
            IntVec3 anchorCell = shipAnchor.Position;

            HashSet<IntVec3> terrainCells = CollectConnectedShipTerrain(map, anchorCell);
            if (terrainCells == null || terrainCells.Count == 0)
                return false;

            List<Thing> structural = CollectStructuralShipThings(map, faction, terrainCells);
            if (structural == null || structural.Count == 0)
                return false;

            HashSet<IntVec3> occupancy = BuildShipOccupancyCells(structural, terrainCells);

            cluster = new ShipClusterData
            {
                map = map,
                faction = faction,
                anchor = shipAnchor,
                anchorCell = anchorCell,
                terrainCells = terrainCells,
                structuralThings = structural,
                occupancyCells = occupancy
            };

            List<Thing> allThings = map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing == null || thing.Destroyed || !thing.Spawned)
                    continue;

                if (!IsThingInsideShipCells(thing, occupancy))
                    continue;

                if (thing.def.category == ThingCategory.Building)
                {
                    cluster.allBuildings.Add(thing);
                    continue;
                }

                if (thing is Pawn pawn)
                {
                    cluster.pawns.Add(pawn);
                    continue;
                }

                cluster.items.Add(thing);
            }

            return true;
        }



        public static bool TryCaptureAndDespawnShip(Thing shipAnchor, string currentNodeId, out ShipSnapshot snapshot)
        {
            snapshot = null;

            if (shipAnchor == null || shipAnchor.Map == null || shipAnchor.def == null)
                return false;

            Map map = shipAnchor.Map;
            Faction faction = shipAnchor.Faction;
            IntVec3 anchorCell = shipAnchor.Position;

            HashSet<IntVec3> terrainCells = CollectConnectedShipTerrain(map, anchorCell);
            if (terrainCells.Count == 0)
                return false;

            List<Thing> structural = CollectStructuralShipThings(map, faction, terrainCells);
            if (structural.Count == 0)
                return false;

            CellRect shipBounds = ComputeBounds(structural, terrainCells);
            shipBounds = shipBounds.ExpandedBy(1);
            shipBounds.ClipInsideMap(map);

            snapshot = new ShipSnapshot
            {
                shipThingId = shipAnchor.thingIDNumber,
                shipDefName = shipAnchor.def.defName,
                currentNodeId = currentNodeId,
                anchorCell = anchorCell
            };

            CaptureTerrain(map, anchorCell, terrainCells, shipBounds, snapshot);

            HashSet<int> capturedThingIds = new HashSet<int>();

            for (int i = 0; i < structural.Count; i++)
            {
                Thing thing = structural[i];
                if (thing == null || thing.Destroyed || !thing.Spawned)
                    continue;

                snapshot.buildings.Add(new ShipThingSnapshot
                {
                    thing = thing,
                    offset = thing.Position - anchorCell
                });
                capturedThingIds.Add(thing.thingIDNumber);
            }

            HashSet<IntVec3> shipOccupancyCells = BuildShipOccupancyCells(structural, terrainCells);
            CaptureRoofs(map, anchorCell, shipBounds, shipOccupancyCells, snapshot);

            List<Thing> allThings = map.listerThings.AllThings.ToList();
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing == null || thing.Destroyed || !thing.Spawned)
                    continue;

                if (capturedThingIds.Contains(thing.thingIDNumber))
                    continue;

                if (thing.def.category == ThingCategory.Building)
                    continue;

                if (!IsThingInsideShipCells(thing, shipOccupancyCells))
                    continue;

                if (thing is Pawn pawn)
                {
                    snapshot.pawns.Add(new ShipPawnSnapshot
                    {
                        pawn = pawn,
                        offset = pawn.Position - anchorCell
                    });
                }
                else
                {
                    snapshot.items.Add(new ShipThingSnapshot
                    {
                        thing = thing,
                        offset = thing.Position - anchorCell
                    });
                }
            }

            HashSet<IntVec3> capturedCells = BuildCapturedCells(map, snapshot);
            IntVec3 cleanupRoot = anchorCell;

            DespawnCapturedThings(snapshot);
            RemoveCapturedRoofsRobust(map, snapshot, capturedCells, cleanupRoot);
            RemoveCapturedTerrainRobust(map, snapshot, capturedCells, cleanupRoot);
            CleanupResidualShipBuildings(map, snapshot, capturedCells);

            Log.Message("[InterstellarOdyssey] Ship captured. Buildings=" + snapshot.buildings.Count
                + " Items=" + snapshot.items.Count
                + " Pawns=" + snapshot.pawns.Count
                + " Terrains=" + snapshot.terrains.Count
                + " Roofs=" + snapshot.roofs.Count);

            return true;
        }

        public static bool IsStructuralShipThing(Thing thing)
        {
            if (thing == null || thing.def == null)
                return false;

            if (thing.def.category != ThingCategory.Building && !(thing is Blueprint) && !(thing is Frame))
                return false;

            string structureDefName = GetShipStructureDefName(thing);
            if (string.IsNullOrEmpty(structureDefName))
                return false;

            return IsKnownShipStructureDef(structureDefName);
        }

        private static int ScorePotentialAnchor(Thing thing, IntVec3 consolePos)
        {
            if (thing == null || thing.def == null)
                return int.MinValue;

            int score = 0;
            string defName = (thing.def.defName ?? string.Empty).ToLowerInvariant();
            string label = (thing.def.label ?? string.Empty).ToLowerInvariant();

            if (defName.Contains("gravengine"))
                score += 500;
            if (defName.Contains("grav"))
                score += 200;
            if (defName.Contains("engine"))
                score += 150;
            if (defName.Contains("bridge") || defName.Contains("console"))
                score += 80;
            if (defName.Contains("hull"))
                score += 40;
            if (label.Contains("грав"))
                score += 100;
            if (label.Contains("кораб"))
                score += 50;

            score -= thing.Position.DistanceToSquared(consolePos);
            return score;
        }

        private static List<Thing> CollectStructuralShipThings(Map map, Faction faction, HashSet<IntVec3> shipCells)
        {
            List<Thing> result = new List<Thing>();
            List<Thing> allThings = map.listerThings.AllThings;

            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing == null || thing.def == null || !thing.Spawned || thing.Destroyed)
                    continue;

                if (thing.def.category != ThingCategory.Building)
                    continue;

                if (faction != null && thing.Faction != faction)
                    continue;

                if (!IsThingConnectedToShipCluster(thing, shipCells))
                    continue;

                if (IsStructuralShipThing(thing) || ThingTouchesShipFloor(thing, shipCells))
                    result.Add(thing);
            }

            return result;
        }

        private static bool IsThingConnectedToShipCluster(Thing thing, HashSet<IntVec3> shipCells)
        {
            if (thing == null || shipCells == null || shipCells.Count == 0)
                return false;

            CellRect rect = thing.OccupiedRect();
            foreach (IntVec3 cell in rect.Cells)
            {
                if (shipCells.Contains(cell))
                    return true;

                for (int i = 0; i < 8; i++)
                {
                    IntVec3 near = cell + GenAdj.AdjacentCells[i];
                    if (shipCells.Contains(near))
                        return true;
                }
            }

            return false;
        }

        private static bool ThingTouchesShipFloor(Thing thing, HashSet<IntVec3> shipCells)
        {
            if (thing == null || shipCells == null)
                return false;

            foreach (IntVec3 cell in thing.OccupiedRect().Cells)
            {
                if (shipCells.Contains(cell))
                    return true;
            }

            return false;
        }

        private static HashSet<IntVec3> BuildShipOccupancyCells(List<Thing> structural, HashSet<IntVec3> terrainCells)
        {
            HashSet<IntVec3> result = new HashSet<IntVec3>();

            if (terrainCells != null)
            {
                foreach (IntVec3 cell in terrainCells)
                    result.Add(cell);
            }

            if (structural != null)
            {
                for (int i = 0; i < structural.Count; i++)
                {
                    Thing thing = structural[i];
                    if (thing == null)
                        continue;

                    foreach (IntVec3 cell in thing.OccupiedRect().Cells)
                        result.Add(cell);
                }
            }

            return result;
        }

        private static bool IsThingInsideShipCells(Thing thing, HashSet<IntVec3> shipCells)
        {
            if (thing == null || shipCells == null || shipCells.Count == 0)
                return false;

            if (thing is Pawn)
                return shipCells.Contains(thing.Position);

            if (thing.def != null && (thing.def.size.x > 1 || thing.def.size.z > 1))
            {
                foreach (IntVec3 cell in thing.OccupiedRect().Cells)
                {
                    if (shipCells.Contains(cell))
                        return true;
                }

                return false;
            }

            return shipCells.Contains(thing.Position);
        }

        private static HashSet<IntVec3> CollectConnectedShipTerrain(Map map, IntVec3 start)
        {
            HashSet<IntVec3> result = new HashSet<IntVec3>();
            if (map == null || !start.InBounds(map))
                return result;

            Queue<IntVec3> open = new Queue<IntVec3>();
            if (ShipFloorUtility.IsShipFloorCell(map, start))
            {
                open.Enqueue(start);
                result.Add(start);
            }
            else
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        IntVec3 at = new IntVec3(start.x + dx, 0, start.z + dz);
                        if (at.InBounds(map) && ShipFloorUtility.IsShipFloorCell(map, at))
                        {
                            open.Enqueue(at);
                            result.Add(at);
                        }
                    }
                }
            }

            while (open.Count > 0)
            {
                IntVec3 cur = open.Dequeue();
                for (int i = 0; i < 8; i++)
                {
                    IntVec3 next = cur + GenAdj.AdjacentCells[i];
                    if (!next.InBounds(map) || result.Contains(next))
                        continue;
                    if (!ShipFloorUtility.IsShipFloorCell(map, next))
                        continue;
                    result.Add(next);
                    open.Enqueue(next);
                }
            }

            return result;
        }

        private static CellRect ComputeBounds(List<Thing> structural, HashSet<IntVec3> terrainCells)
        {
            bool hasAny = false;
            int minX = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxZ = int.MinValue;

            foreach (Thing thing in structural)
            {
                if (thing == null)
                    continue;

                foreach (IntVec3 cell in thing.OccupiedRect().Cells)
                {
                    hasAny = true;
                    minX = Mathf.Min(minX, cell.x);
                    minZ = Mathf.Min(minZ, cell.z);
                    maxX = Mathf.Max(maxX, cell.x);
                    maxZ = Mathf.Max(maxZ, cell.z);
                }
            }

            foreach (IntVec3 cell in terrainCells)
            {
                hasAny = true;
                minX = Mathf.Min(minX, cell.x);
                minZ = Mathf.Min(minZ, cell.z);
                maxX = Mathf.Max(maxX, cell.x);
                maxZ = Mathf.Max(maxZ, cell.z);
            }

            if (!hasAny)
                return CellRect.Empty;

            return CellRect.FromLimits(minX, minZ, maxX, maxZ);
        }

        private static void CaptureTerrain(Map map, IntVec3 anchorCell, HashSet<IntVec3> terrainCells, CellRect shipBounds, ShipSnapshot snapshot)
        {
            if (map == null || snapshot == null)
                return;

            HashSet<IntVec3> cellsToCapture = new HashSet<IntVec3>();

            if (terrainCells != null)
            {
                foreach (IntVec3 cell in terrainCells)
                {
                    if (cell.InBounds(map))
                        cellsToCapture.Add(cell);
                }
            }

            if (!shipBounds.IsEmpty)
            {
                CellRect expandedBounds = shipBounds.ExpandedBy(2);
                expandedBounds.ClipInsideMap(map);

                foreach (IntVec3 cell in expandedBounds.Cells)
                {
                    if (!cell.InBounds(map))
                        continue;

                    TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                    if (ShipFloorUtility.IsShipFloor(terrain))
                        cellsToCapture.Add(cell);
                }
            }

            foreach (IntVec3 cell in cellsToCapture)
            {
                if (!cell.InBounds(map))
                    continue;

                TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                if (!ShipFloorUtility.IsShipFloor(terrain))
                    continue;

                snapshot.terrains.Add(new ShipTerrainSnapshot
                {
                    offset = cell - anchorCell,
                    terrainDef = terrain
                });
            }
        }

        private static void CaptureRoofs(Map map, IntVec3 anchorCell, CellRect shipBounds, HashSet<IntVec3> roofCells, ShipSnapshot snapshot)
        {
            if (map == null || map.roofGrid == null || snapshot == null)
                return;

            HashSet<IntVec3> cellsToCapture = new HashSet<IntVec3>();

            if (roofCells != null)
            {
                foreach (IntVec3 cell in roofCells)
                {
                    if (cell.InBounds(map))
                        cellsToCapture.Add(cell);
                }
            }

            if (!shipBounds.IsEmpty)
            {
                CellRect expandedBounds = shipBounds.ExpandedBy(1);
                expandedBounds.ClipInsideMap(map);

                foreach (IntVec3 cell in expandedBounds.Cells)
                {
                    if (cell.InBounds(map) && map.roofGrid.RoofAt(cell) != null)
                        cellsToCapture.Add(cell);
                }
            }

            foreach (IntVec3 cell in cellsToCapture)
            {
                if (!cell.InBounds(map))
                    continue;

                RoofDef roof = map.roofGrid.RoofAt(cell);
                if (roof == null)
                    continue;

                snapshot.roofs.Add(new ShipRoofSnapshot
                {
                    offset = cell - anchorCell,
                    roofDef = roof
                });
            }
        }

        private static HashSet<IntVec3> BuildCapturedCells(Map map, ShipSnapshot snapshot)
        {
            HashSet<IntVec3> cells = new HashSet<IntVec3>();

            if (map == null || snapshot == null)
                return cells;

            if (snapshot.terrains != null)
            {
                for (int i = 0; i < snapshot.terrains.Count; i++)
                {
                    IntVec3 cell = snapshot.anchorCell + snapshot.terrains[i].offset;
                    if (cell.InBounds(map))
                        cells.Add(cell);
                }
            }

            if (snapshot.roofs != null)
            {
                for (int i = 0; i < snapshot.roofs.Count; i++)
                {
                    IntVec3 cell = snapshot.anchorCell + snapshot.roofs[i].offset;
                    if (cell.InBounds(map))
                        cells.Add(cell);
                }
            }

            if (snapshot.buildings != null)
            {
                for (int i = 0; i < snapshot.buildings.Count; i++)
                {
                    Thing thing = snapshot.buildings[i].thing;
                    if (thing == null || thing.def == null)
                        continue;

                    IntVec3 pos = snapshot.anchorCell + snapshot.buildings[i].offset;
                    CellRect rect = GenAdj.OccupiedRect(pos, thing.Rotation, thing.def.Size);

                    foreach (IntVec3 cell in rect.Cells)
                    {
                        if (cell.InBounds(map))
                            cells.Add(cell);
                    }
                }
            }

            if (snapshot.items != null)
            {
                for (int i = 0; i < snapshot.items.Count; i++)
                {
                    IntVec3 cell = snapshot.anchorCell + snapshot.items[i].offset;
                    if (cell.InBounds(map))
                        cells.Add(cell);
                }
            }

            if (snapshot.pawns != null)
            {
                for (int i = 0; i < snapshot.pawns.Count; i++)
                {
                    IntVec3 cell = snapshot.anchorCell + snapshot.pawns[i].offset;
                    if (cell.InBounds(map))
                        cells.Add(cell);
                }
            }

            return cells;
        }

        private static void DespawnCapturedThings(ShipSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            if (snapshot.pawns != null)
            {
                for (int i = 0; i < snapshot.pawns.Count; i++)
                {
                    Pawn pawn = snapshot.pawns[i].pawn;
                    if (pawn == null || pawn.Destroyed || !pawn.Spawned)
                        continue;

                    pawn.DeSpawn();
                }
            }

            if (snapshot.items != null)
            {
                for (int i = 0; i < snapshot.items.Count; i++)
                {
                    Thing thing = snapshot.items[i].thing;
                    if (thing == null || thing.Destroyed || !thing.Spawned)
                        continue;

                    thing.DeSpawn();
                }
            }

            if (snapshot.buildings != null)
            {
                for (int i = 0; i < snapshot.buildings.Count; i++)
                {
                    Thing thing = snapshot.buildings[i].thing;
                    if (thing == null || thing.Destroyed || !thing.Spawned)
                        continue;

                    thing.DeSpawn();
                }
            }
        }


        private static void RemoveCapturedRoofsRobust(Map map, ShipSnapshot snapshot, HashSet<IntVec3> capturedCells, IntVec3 cleanupRoot)
        {
            if (map == null || map.roofGrid == null)
                return;

            HashSet<IntVec3> cellsToClear = new HashSet<IntVec3>();

            if (capturedCells != null)
            {
                foreach (IntVec3 cell in capturedCells)
                {
                    if (cell.InBounds(map))
                        cellsToClear.Add(cell);
                }
            }

            if (snapshot != null && snapshot.roofs != null)
            {
                for (int i = 0; i < snapshot.roofs.Count; i++)
                {
                    ShipRoofSnapshot entry = snapshot.roofs[i];
                    if (entry == null || entry.roofDef == null)
                        continue;

                    IntVec3 cell = snapshot.anchorCell + entry.offset;
                    if (cell.InBounds(map))
                        cellsToClear.Add(cell);
                }
            }

            foreach (IntVec3 cell in CollectConnectedShipFloorForCleanup(map, cleanupRoot, 4))
                cellsToClear.Add(cell);

            List<IntVec3> seeds = cellsToClear.ToList();
            for (int i = 0; i < seeds.Count; i++)
            {
                IntVec3 c = seeds[i];
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        IntVec3 at = new IntVec3(c.x + dx, 0, c.z + dz);
                        if (!at.InBounds(map))
                            continue;

                        RoofDef roof = map.roofGrid.RoofAt(at);
                        if (roof != null)
                            cellsToClear.Add(at);
                    }
                }
            }

            foreach (IntVec3 cell in cellsToClear)
            {
                if (!cell.InBounds(map))
                    continue;

                RoofDef currentRoof = map.roofGrid.RoofAt(cell);
                if (currentRoof == null)
                    continue;

                try
                {
                    map.roofGrid.SetRoof(cell, null);
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Roofs);
                }
                catch (Exception ex)
                {
                    Log.Warning("[InterstellarOdyssey] RemoveCapturedRoofsRobust failed at " + cell + ": " + ex);
                }
            }
        }

        private static void RemoveCapturedTerrainRobust(Map map, ShipSnapshot snapshot, HashSet<IntVec3> capturedCells, IntVec3 cleanupRoot)
        {
            if (map == null)
                return;

            Log.Message("[IO] RemoveCapturedTerrainRobust start. snapshotTerrains="
                + (snapshot?.terrains?.Count ?? 0)
                + " capturedCells=" + (capturedCells?.Count ?? 0)
                + " cleanupRoot=" + cleanupRoot);

            HashSet<IntVec3> cellsToClear = new HashSet<IntVec3>();
            Dictionary<IntVec3, TerrainDef> replacementByCell = new Dictionary<IntVec3, TerrainDef>();

            if (snapshot != null && snapshot.terrains != null)
            {
                for (int i = 0; i < snapshot.terrains.Count; i++)
                {
                    ShipTerrainSnapshot entry = snapshot.terrains[i];
                    if (entry == null)
                        continue;

                    IntVec3 cell = snapshot.anchorCell + entry.offset;
                    if (cell.InBounds(map))
                        cellsToClear.Add(cell);
                }
            }

            if (capturedCells != null)
            {
                foreach (IntVec3 cell in capturedCells)
                {
                    if (cell.InBounds(map))
                        cellsToClear.Add(cell);
                }
            }

            CellRect sourceBounds = ComputeSourceCleanupBounds(map, snapshot);
            if (!sourceBounds.IsEmpty)
            {
                foreach (IntVec3 cell in sourceBounds.Cells)
                {
                    if (!cell.InBounds(map))
                        continue;

                    TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                    if (ShipFloorUtility.IsShipFloor(terrain))
                        cellsToClear.Add(cell);
                }
            }

            foreach (IntVec3 cell in CollectConnectedShipFloorForCleanup(map, cleanupRoot, 24))
            {
                if (cell.InBounds(map))
                    cellsToClear.Add(cell);
            }

            foreach (IntVec3 cell in cellsToClear)
            {
                if (!cell.InBounds(map))
                    continue;

                TerrainDef current = map.terrainGrid.TerrainAt(cell);
                if (current == null || !ShipFloorUtility.IsShipFloor(current))
                    continue;

                replacementByCell[cell] = ResolveReplacementTerrainForCleanup(map, cell, cellsToClear);
            }

            int removedCount = 0;
            int fallbackCount = 0;

            foreach (IntVec3 cell in cellsToClear)
            {
                if (!cell.InBounds(map))
                    continue;

                TerrainDef current = map.terrainGrid.TerrainAt(cell);
                if (current == null || !ShipFloorUtility.IsShipFloor(current))
                    continue;

                TerrainDef replacement = replacementByCell.TryGetValue(cell, out TerrainDef planned)
                    ? planned
                    : (TerrainDefOf.Soil ?? TerrainDefOf.Sand);

                try
                {
                    bool cleared = false;

                    if (current.layerable)
                    {
                        try
                        {
                            map.terrainGrid.RemoveTopLayer(cell, false);
                            cleared = true;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("[InterstellarOdyssey] RemoveTopLayer failed at " + cell + ": " + ex.Message);
                        }
                    }

                    TerrainDef afterRemove = map.terrainGrid.TerrainAt(cell);
                    if (afterRemove == null || ShipFloorUtility.IsShipFloor(afterRemove))
                    {
                        map.terrainGrid.SetTerrain(cell, replacement);
                        fallbackCount++;
                    }

                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Terrain);
                    removedCount++;
                }
                catch (Exception ex)
                {
                    Log.Warning("[InterstellarOdyssey] RemoveCapturedTerrainRobust cleanup failed at " + cell + ": " + ex);
                }
            }

            Log.Message("[IO] RemoveCapturedTerrainRobust done. cellsToClear="
                + cellsToClear.Count + " removedCount=" + removedCount + " fallbackCount=" + fallbackCount);
        }

        private static TerrainDef ResolveReplacementTerrainForCleanup(Map map, IntVec3 cell, HashSet<IntVec3> cellsBeingRemoved)
        {
            if (map == null || !cell.InBounds(map))
                return TerrainDefOf.Soil ?? TerrainDefOf.Sand;

            for (int radius = 1; radius <= 12; radius++)
            {
                CellRect rect = CellRect.CenteredOn(cell, radius);
                rect.ClipInsideMap(map);

                foreach (IntVec3 at in rect.EdgeCells)
                {
                    if (!at.InBounds(map))
                        continue;

                    if (cellsBeingRemoved != null && cellsBeingRemoved.Contains(at))
                        continue;

                    TerrainDef terrain = map.terrainGrid.TerrainAt(at);
                    if (terrain == null)
                        continue;

                    if (!ShipFloorUtility.IsShipFloor(terrain))
                        return terrain;
                }
            }

            return TerrainDefOf.Soil ?? TerrainDefOf.Sand;
        }

        private static CellRect ComputeSourceCleanupBounds(Map map, ShipSnapshot snapshot)
        {
            if (map == null || snapshot == null)
                return CellRect.Empty;

            bool hasAny = false;
            int minX = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxZ = int.MinValue;

            if (snapshot.terrains != null)
            {
                for (int i = 0; i < snapshot.terrains.Count; i++)
                {
                    ShipTerrainSnapshot entry = snapshot.terrains[i];
                    if (entry == null)
                        continue;

                    IntVec3 cell = snapshot.anchorCell + entry.offset;
                    if (!cell.InBounds(map))
                        continue;

                    hasAny = true;
                    minX = Mathf.Min(minX, cell.x);
                    minZ = Mathf.Min(minZ, cell.z);
                    maxX = Mathf.Max(maxX, cell.x);
                    maxZ = Mathf.Max(maxZ, cell.z);
                }
            }

            if (snapshot.roofs != null)
            {
                for (int i = 0; i < snapshot.roofs.Count; i++)
                {
                    ShipRoofSnapshot entry = snapshot.roofs[i];
                    if (entry == null)
                        continue;

                    IntVec3 cell = snapshot.anchorCell + entry.offset;
                    if (!cell.InBounds(map))
                        continue;

                    hasAny = true;
                    minX = Mathf.Min(minX, cell.x);
                    minZ = Mathf.Min(minZ, cell.z);
                    maxX = Mathf.Max(maxX, cell.x);
                    maxZ = Mathf.Max(maxZ, cell.z);
                }
            }

            if (snapshot.buildings != null)
            {
                for (int i = 0; i < snapshot.buildings.Count; i++)
                {
                    ShipThingSnapshot entry = snapshot.buildings[i];
                    Thing thing = entry != null ? entry.thing : null;
                    if (thing == null || thing.def == null)
                        continue;

                    IntVec3 pos = snapshot.anchorCell + entry.offset;
                    CellRect rect = GenAdj.OccupiedRect(pos, thing.Rotation, thing.def.Size);

                    foreach (IntVec3 cell in rect.Cells)
                    {
                        if (!cell.InBounds(map))
                            continue;

                        hasAny = true;
                        minX = Mathf.Min(minX, cell.x);
                        minZ = Mathf.Min(minZ, cell.z);
                        maxX = Mathf.Max(maxX, cell.x);
                        maxZ = Mathf.Max(maxZ, cell.z);
                    }
                }
            }

            if (!hasAny)
                return CellRect.Empty;

            CellRect bounds = CellRect.FromLimits(minX, minZ, maxX, maxZ).ExpandedBy(2);
            bounds.ClipInsideMap(map);
            return bounds;
        }

        private static HashSet<IntVec3> CollectConnectedShipFloorForCleanup(Map map, IntVec3 root, int searchRadius)
        {
            HashSet<IntVec3> result = new HashSet<IntVec3>();
            if (map == null)
                return result;

            Queue<IntVec3> open = new Queue<IntVec3>();

            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                for (int dz = -searchRadius; dz <= searchRadius; dz++)
                {
                    IntVec3 at = new IntVec3(root.x + dx, 0, root.z + dz);
                    if (!at.InBounds(map))
                        continue;

                    TerrainDef terrain = map.terrainGrid.TerrainAt(at);
                    if (!ShipFloorUtility.IsShipFloor(terrain))
                        continue;

                    if (result.Add(at))
                        open.Enqueue(at);
                }
            }

            while (open.Count > 0)
            {
                IntVec3 cur = open.Dequeue();

                for (int i = 0; i < 8; i++)
                {
                    IntVec3 next = cur + GenAdj.AdjacentCells[i];
                    if (!next.InBounds(map))
                        continue;

                    if (result.Contains(next))
                        continue;

                    TerrainDef terrain = map.terrainGrid.TerrainAt(next);
                    if (!ShipFloorUtility.IsShipFloor(terrain))
                        continue;

                    result.Add(next);
                    open.Enqueue(next);
                }
            }

            return result;
        }

        private static TerrainDef ResolveReplacementTerrain(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
                return TerrainDefOf.Soil ?? TerrainDefOf.Sand;

            for (int radius = 1; radius <= 12; radius++)
            {
                CellRect rect = CellRect.CenteredOn(cell, radius);
                rect.ClipInsideMap(map);

                foreach (IntVec3 at in rect.EdgeCells)
                {
                    if (!at.InBounds(map))
                        continue;

                    TerrainDef terrain = map.terrainGrid.TerrainAt(at);
                    if (terrain == null)
                        continue;

                    // ВАЖНО: игнорируем корабельные полы
                    if (!ShipFloorUtility.IsShipFloor(terrain))
                        return terrain;
                }
            }

            // Если вокруг только корабельный пол — возвращаем обычную землю
            return TerrainDefOf.Soil ?? TerrainDefOf.Sand;
        }

        private static void CleanupResidualShipBuildings(Map map, ShipSnapshot snapshot, HashSet<IntVec3> capturedCells)
        {
            if (map == null || snapshot == null || capturedCells == null)
                return;

            HashSet<string> capturedStructureDefs = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot.buildings != null)
            {
                for (int i = 0; i < snapshot.buildings.Count; i++)
                {
                    ShipThingSnapshot entry = snapshot.buildings[i];
                    string defName = GetShipStructureDefName(entry != null ? entry.thing : null);
                    if (!string.IsNullOrEmpty(defName))
                        capturedStructureDefs.Add(defName);
                }
            }

            if (capturedStructureDefs.Count == 0)
                return;

            HashSet<int> processedThingIds = new HashSet<int>();

            foreach (IntVec3 cell in capturedCells)
            {
                if (!cell.InBounds(map))
                    continue;

                List<Thing> things = map.thingGrid.ThingsListAt(cell);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    Thing thing = things[i];
                    if (thing == null || thing.Destroyed)
                        continue;

                    if (!processedThingIds.Add(thing.thingIDNumber))
                        continue;

                    if (!IsResidualShipPart(thing, capturedStructureDefs, capturedCells))
                        continue;

                    thing.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private static bool IsResidualShipPart(Thing thing, HashSet<string> capturedStructureDefs, HashSet<IntVec3> capturedCells)
        {
            if (thing == null || thing.def == null || capturedStructureDefs == null || capturedStructureDefs.Count == 0)
                return false;

            string defName = GetShipStructureDefName(thing);
            if (string.IsNullOrEmpty(defName) || !capturedStructureDefs.Contains(defName))
                return false;

            if (thing.def.category != ThingCategory.Building && !(thing is Blueprint) && !(thing is Frame))
                return false;

            foreach (IntVec3 cell in thing.OccupiedRect().Cells)
            {
                if (capturedCells.Contains(cell))
                    return true;
            }

            return false;
        }
    }

    public static class ShipLandingUtility
    {
        public static bool TryRestoreShip(ShipSnapshot snapshot, Map map, IntVec3 targetCenter, out Thing restoredAnchor)
        {
            restoredAnchor = null;

            if (snapshot == null || map == null)
                return false;

            RestoreTerrain(snapshot, map, targetCenter);
            RestoreBuildings(snapshot, map, targetCenter, ref restoredAnchor);
            RestoreRoofs(snapshot, map, targetCenter);
            RestoreItems(snapshot, map, targetCenter);
            RestorePawns(snapshot, map, targetCenter);

            return true;
        }

        private static void RestoreTerrain(ShipSnapshot snapshot, Map map, IntVec3 targetCenter)
        {
            if (snapshot.terrains == null)
                return;

            for (int i = 0; i < snapshot.terrains.Count; i++)
            {
                ShipTerrainSnapshot entry = snapshot.terrains[i];
                if (entry == null || entry.terrainDef == null)
                    continue;

                if (!ShipFloorUtility.IsRestoreableShipTerrain(entry.terrainDef))
                    continue;

                IntVec3 cell = targetCenter + entry.offset;
                if (!cell.InBounds(map))
                    continue;

                if (map.roofGrid != null)
                    map.roofGrid.SetRoof(cell, null);

                TerrainDef current = map.terrainGrid.TerrainAt(cell);
                if (current == entry.terrainDef)
                    continue;

                try
                {
                    map.terrainGrid.SetTerrain(cell, entry.terrainDef);
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Terrain);
                }
                catch (Exception ex)
                {
                    Log.Warning("[InterstellarOdyssey] RestoreTerrain skipped at " + cell + ": " + ex.Message);
                }
            }
        }


        private static void RestoreRoofs(ShipSnapshot snapshot, Map map, IntVec3 targetCenter)
        {
            if (snapshot == null || map == null || map.roofGrid == null || snapshot.roofs == null)
                return;

            HashSet<IntVec3> pendingCells = new HashSet<IntVec3>();

            for (int i = 0; i < snapshot.roofs.Count; i++)
            {
                ShipRoofSnapshot entry = snapshot.roofs[i];
                if (entry == null || entry.roofDef == null)
                    continue;

                IntVec3 cell = targetCenter + entry.offset;
                if (cell.InBounds(map))
                    pendingCells.Add(cell);
            }

            foreach (IntVec3 cell in pendingCells.OrderBy(c => c.x).ThenBy(c => c.z))
            {
                ShipRoofSnapshot entry = snapshot.roofs.FirstOrDefault(r => r != null && r.roofDef != null && targetCenter + r.offset == cell);
                if (entry == null)
                    continue;

                try
                {
                    map.roofGrid.SetRoof(cell, entry.roofDef);
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Roofs);
                }
                catch (Exception ex)
                {
                    Log.Warning("[InterstellarOdyssey] RestoreRoofs skipped at " + cell + ": " + ex.Message);
                }
            }
        }

        private static void RestoreBuildings(ShipSnapshot snapshot, Map map, IntVec3 targetCenter, ref Thing restoredAnchor)
        {
            if (snapshot.buildings == null)
                return;

            for (int i = 0; i < snapshot.buildings.Count; i++)
            {
                ShipThingSnapshot entry = snapshot.buildings[i];
                if (entry == null || entry.thing == null || entry.thing.def == null)
                    continue;

                IntVec3 cell = targetCenter + entry.offset;
                if (!cell.InBounds(map))
                    continue;

                Thing thing = entry.thing;
                thing.Position = cell;

                if (!thing.Spawned && GenSpawn.Spawn(thing, cell, map, thing.Rotation, WipeMode.Vanish) != null)
                {
                    if (restoredAnchor == null && thing.def.defName == snapshot.shipDefName)
                        restoredAnchor = thing;
                }
            }
        }

        private static void RestoreItems(ShipSnapshot snapshot, Map map, IntVec3 targetCenter)
        {
            if (snapshot.items == null)
                return;

            for (int i = 0; i < snapshot.items.Count; i++)
            {
                ShipThingSnapshot entry = snapshot.items[i];
                if (entry == null || entry.thing == null || entry.thing.def == null)
                    continue;

                Thing thing = entry.thing;
                if (thing.Spawned)
                    continue;

                IntVec3 desiredCell = targetCenter + entry.offset;
                if (!TryFindRestoreCellForThing(map, desiredCell, out IntVec3 spawnCell))
                {
                    Log.Warning("[InterstellarOdyssey] RestoreItems skipped for " + thing.LabelCap + ": no valid spawn cell near " + desiredCell);
                    continue;
                }

                try
                {
                    thing.Position = spawnCell;
                    GenSpawn.Spawn(thing, spawnCell, map, WipeMode.Vanish);
                }
                catch (Exception ex)
                {
                    Log.Warning("[InterstellarOdyssey] RestoreItems failed for " + thing.LabelCap + " at " + spawnCell + ": " + ex.Message);
                }
            }
        }

        private static void RestorePawns(ShipSnapshot snapshot, Map map, IntVec3 targetCenter)
        {
            if (snapshot.pawns == null)
                return;

            for (int i = 0; i < snapshot.pawns.Count; i++)
            {
                ShipPawnSnapshot entry = snapshot.pawns[i];
                if (entry == null || entry.pawn == null)
                    continue;

                Pawn pawn = entry.pawn;
                if (pawn.Spawned)
                    continue;

                IntVec3 desiredCell = targetCenter + entry.offset;
                if (!TryFindRestoreCellForPawn(map, desiredCell, out IntVec3 spawnCell))
                {
                    Log.Warning("[InterstellarOdyssey] RestorePawns skipped for " + pawn.LabelCap + ": no valid spawn cell near " + desiredCell);
                    continue;
                }

                try
                {
                    GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);
                }
                catch (Exception ex)
                {
                    Log.Warning("[InterstellarOdyssey] RestorePawns failed for " + pawn.LabelCap + " at " + spawnCell + ": " + ex.Message);
                }
            }
        }

        private static bool TryFindRestoreCellForThing(Map map, IntVec3 desiredCell, out IntVec3 result)
        {
            return TryFindNearbyCell(map, desiredCell, 6, IsCellUsableForThing, out result);
        }

        private static bool TryFindRestoreCellForPawn(Map map, IntVec3 desiredCell, out IntVec3 result)
        {
            return TryFindNearbyCell(map, desiredCell, 8, IsCellUsableForPawn, out result);
        }

        private static bool TryFindNearbyCell(Map map, IntVec3 desiredCell, int maxRadius, Func<Map, IntVec3, bool> validator, out IntVec3 result)
        {
            result = IntVec3.Invalid;

            if (map == null || validator == null)
                return false;

            if (validator(map, desiredCell))
            {
                result = desiredCell;
                return true;
            }

            int cellCount = GenRadial.NumCellsInRadius(maxRadius);
            for (int i = 0; i < cellCount; i++)
            {
                IntVec3 cell = desiredCell + GenRadial.RadialPattern[i];
                if (!validator(map, cell))
                    continue;

                result = cell;
                return true;
            }

            return false;
        }

        private static bool IsCellUsableForThing(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
                return false;

            if (map.fogGrid != null && map.fogGrid.IsFogged(cell))
                return false;

            if (!cell.Walkable(map))
                return false;

            return true;
        }

        private static bool IsCellUsableForPawn(Map map, IntVec3 cell)
        {
            if (!IsCellUsableForThing(map, cell))
                return false;

            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || thing.Destroyed)
                    continue;

                if (thing is Pawn)
                    return false;

                if (thing.def != null && thing.def.passability == Traversability.Impassable)
                    return false;
            }

            return true;
        }
    }

    public class WorldComponent_Interstellar : WorldComponent
    {
        public List<OrbitalNode> nodes = new List<OrbitalNode>();
        public List<ShipTransitRecord> activeTravels = new List<ShipTransitRecord>();
        public bool generated;

        public WorldComponent_Interstellar(World world) : base(world)
        {
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            GenerateIfNeeded();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (Find.TickManager == null)
                return;

            for (int i = activeTravels.Count - 1; i >= 0; i--)
            {
                ShipTransitRecord travel = activeTravels[i];
                if (travel.stage != InterstellarTransitStage.InTransit)
                    continue;

                if (Find.TickManager.TicksGame >= travel.arrivalTick)
                    Arrive(travel);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref generated, "generated", false);
            Scribe_Collections.Look(ref nodes, "nodes", LookMode.Deep);
            Scribe_Collections.Look(ref activeTravels, "activeTravels", LookMode.Deep);

            if (nodes == null) nodes = new List<OrbitalNode>();
            if (activeTravels == null) activeTravels = new List<ShipTransitRecord>();
        }

        public void GenerateIfNeeded()
        {
            if (generated && nodes.Count > 0)
                return;

            nodes.Clear();

            string planetName = Find.World != null && Find.World.info != null
                ? Find.World.info.name
                : "RimWorld";

            nodes.Add(new OrbitalNode("homeworld", planetName, OrbitalNodeType.Planet, 0f, 75f));
            nodes.Add(new OrbitalNode("ares", "Ares", OrbitalNodeType.Planet, 160f, 130f));
            nodes.Add(new OrbitalNode("nivalis", "Nivalis", OrbitalNodeType.Planet, 320f, 185f));
            nodes.Add(new OrbitalNode("station", "Орбитальная станция", OrbitalNodeType.Station, 75f, 45f));
            nodes.Add(new OrbitalNode("belt", "Пояс астероидов", OrbitalNodeType.AsteroidBelt, 235f, 230f));

            generated = true;
        }

        public bool IsShipTravelling(Thing ship)
        {
            if (ship == null)
                return false;

            return activeTravels.Any(t => t.shipThingId == ship.thingIDNumber && t.stage != InterstellarTransitStage.None);
        }

        public OrbitalNode GetCurrentNodeForShip(Thing ship)
        {
            if (ship == null)
                return GetNodeById("homeworld") ?? nodes.FirstOrDefault();

            ShipTransitRecord record = activeTravels.FirstOrDefault(t => t.shipThingId == ship.thingIDNumber);
            if (record != null)
                return GetNodeById(record.destinationId) ?? GetNodeById(record.sourceId);

            return GetNodeById("homeworld") ?? nodes.FirstOrDefault();
        }

        public OrbitalNode GetNodeById(string id)
        {
            return nodes.FirstOrDefault(n => n.id == id);
        }

        public string ResolveNodeLabel(OrbitalNode node)
        {
            return node != null ? node.label : "Неизвестно";
        }

        public IEnumerable<ShipTransitRecord> GetLandingReadyTravels()
        {
            return activeTravels.Where(t => t.stage == InterstellarTransitStage.AwaitingLanding);
        }

        public bool StartTravel(Thing shipAnchor, OrbitalNode destination)
        {
            if (shipAnchor == null || destination == null)
                return false;

            if (IsShipTravelling(shipAnchor))
            {
                Messages.Message("Корабль уже находится в перелёте.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            OrbitalNode current = GetCurrentNodeForShip(shipAnchor);
            if (current != null && current.id == destination.id)
            {
                Messages.Message("Корабль уже находится у этой цели.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            ShipValidationReport validation = ShipValidationUtility.ValidateForLaunch(shipAnchor);
            if (!validation.CanLaunch)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(validation.ToUserText()));
                return false;
            }

            if (!ShipCaptureUtility.TryCaptureAndDespawnShip(shipAnchor, current != null ? current.id : "homeworld", out ShipSnapshot snapshot))
            {
                Messages.Message("Не удалось захватить корабль для перелёта.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            float days = Mathf.Max(0.2f, OrbitalMath.Distance(current, destination) / 45f);
            int durationTicks = Mathf.Max(2500, Mathf.RoundToInt(days * GenDate.TicksPerDay));

            ShipTransitRecord record = new ShipTransitRecord
            {
                shipThingId = shipAnchor.thingIDNumber,
                shipLabel = shipAnchor.LabelCap,
                shipDefName = shipAnchor.def.defName,
                sourceId = current != null ? current.id : "homeworld",
                destinationId = destination.id,
                departureTick = Find.TickManager.TicksGame,
                arrivalTick = Find.TickManager.TicksGame + durationTicks,
                stage = InterstellarTransitStage.InTransit,
                snapshot = snapshot
            };

            activeTravels.Add(record);
            Messages.Message("Начат межпланетный перелёт: " + record.shipLabel + " → " + ResolveNodeLabel(destination), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public bool TryLandShip(ShipTransitRecord record, Map map)
        {
            if (record == null || map == null || record.snapshot == null)
                return false;

            IntVec3 center = CellFinderLoose.RandomCellWith(c => c.Standable(map), map, 2000);
            if (!center.IsValid)
                center = map.Center;

            if (!ShipLandingUtility.TryRestoreShip(record.snapshot, map, center, out Thing restoredAnchor))
                return false;

            record.stage = InterstellarTransitStage.None;
            activeTravels.Remove(record);

            OrbitalNode destination = GetNodeById(record.destinationId);
            Messages.Message("Корабль " + (record.shipLabel ?? "без названия") + " совершил посадку у цели " + ResolveNodeLabel(destination) + ".", MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        private void Arrive(ShipTransitRecord record)
        {
            record.stage = InterstellarTransitStage.AwaitingLanding;
            if (record.snapshot != null)
                record.snapshot.currentNodeId = record.destinationId;

            OrbitalNode destination = GetNodeById(record.destinationId);
            Messages.Message("Корабль вышел на орбиту цели: " + ResolveNodeLabel(destination), MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class Window_OrbitalMap : Window
    {
        private readonly Thing ship;
        private ShipValidationReport validationReport;
        private Vector2 scrollPos;

        private WorldComponent_Interstellar Data
        {
            get { return Find.World.GetComponent<WorldComponent_Interstellar>(); }
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(980f, 720f); }
        }

        public Window_OrbitalMap(Thing ship)
        {
            this.ship = ship;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            draggable = true;
            Data.GenerateIfNeeded();
            RefreshValidationReport();
        }

        private void RefreshValidationReport()
        {
            validationReport = ShipValidationUtility.ValidateForLaunch(ship);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "Орбитальная карта");
            Text.Font = GameFont.Small;

            Rect topInfo = new Rect(inRect.x, titleRect.yMax + 10f, inRect.width - 170f, 30f);
            OrbitalNode current = Data.GetCurrentNodeForShip(ship);
            Widgets.Label(topInfo, "Текущая цель: " + Data.ResolveNodeLabel(current));

            Rect refreshButtonRect = new Rect(inRect.xMax - 150f, titleRect.yMax + 6f, 150f, 32f);
            if (Widgets.ButtonText(refreshButtonRect, "Обновить проверку"))
                RefreshValidationReport();

            Rect leftRect = new Rect(inRect.x, topInfo.yMax + 10f, inRect.width * 0.58f, inRect.height - 95f);
            Rect rightRect = new Rect(leftRect.xMax + 12f, topInfo.yMax + 10f, inRect.width - leftRect.width - 12f, inRect.height - 95f);

            float checklistHeight = Mathf.Min(250f, rightRect.height * 0.42f);
            Rect checklistRect = new Rect(rightRect.x, rightRect.y, rightRect.width, checklistHeight);
            Rect destinationsRect = new Rect(rightRect.x, checklistRect.yMax + 10f, rightRect.width, rightRect.height - checklistHeight - 10f);

            DrawOrbitCanvas(leftRect);
            DrawValidationChecklist(checklistRect);
            DrawDestinationList(destinationsRect, current);

            if (Data.IsShipTravelling(ship))
            {
                Rect bottomRect = new Rect(inRect.x, inRect.yMax - 28f, inRect.width, 28f);
                Widgets.Label(bottomRect, "Этот корабль уже находится в перелёте.");
            }
        }

        private void DrawOrbitCanvas(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect inner = rect.ContractedBy(10f);
            GUI.BeginGroup(inner);

            Vector2 center = new Vector2(inner.width / 2f, inner.height / 2f);
            float scale = Mathf.Min(inner.width, inner.height) / 520f;

            foreach (OrbitalNode node in Data.nodes)
                if (node.radius > 1f)
                    DrawOrbitRing(center, node.radius * scale);

            Rect starRect = new Rect(center.x - 16f, center.y - 16f, 32f, 32f);
            Widgets.DrawBoxSolid(starRect, new Color(1f, 0.78f, 0.2f));

            foreach (OrbitalNode node in Data.nodes)
                DrawNode(node, center, scale);

            GUI.EndGroup();
        }

        private void DrawOrbitRing(Vector2 center, float radius)
        {
            int segments = 72;
            Vector2 prev = center + new Vector2(radius, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = (360f / segments) * i * Mathf.Deg2Rad;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Widgets.DrawLine(prev, next, Color.gray, 1f);
                prev = next;
            }
        }

        private void DrawNode(OrbitalNode node, Vector2 center, float scale)
        {
            Vector2 pos = OrbitalMath.Position(node) * scale;
            Vector2 drawPos = center + pos;

            float size = 14f;
            Color color = Color.white;

            switch (node.type)
            {
                case OrbitalNodeType.Planet:
                    color = new Color(0.35f, 0.75f, 1f);
                    size = 16f;
                    break;
                case OrbitalNodeType.Station:
                    color = new Color(0.8f, 0.8f, 0.85f);
                    size = 12f;
                    break;
                case OrbitalNodeType.Asteroid:
                case OrbitalNodeType.AsteroidBelt:
                    color = new Color(0.65f, 0.55f, 0.45f);
                    size = 12f;
                    break;
            }

            Rect nodeRect = new Rect(drawPos.x - size / 2f, drawPos.y - size / 2f, size, size);
            Widgets.DrawBoxSolid(nodeRect, color);

            Rect labelRect = new Rect(drawPos.x + 8f, drawPos.y - 10f, 180f, 24f);
            Widgets.Label(labelRect, Data.ResolveNodeLabel(node));
        }


        private void DrawValidationChecklist(Rect rect)
        {
            if (validationReport == null)
                RefreshValidationReport();

            Widgets.DrawMenuSection(rect);

            Rect inner = rect.ContractedBy(10f);
            Rect header = new Rect(inner.x, inner.y, inner.width - 100f, 25f);
            Widgets.Label(header, "Предстартовая проверка");

            Rect stateRect = new Rect(inner.xMax - 100f, inner.y, 100f, 25f);
            Color prevColor = GUI.color;
            GUI.color = validationReport != null && validationReport.CanLaunch ? new Color(0.45f, 1f, 0.45f) : new Color(1f, 0.45f, 0.45f);
            Widgets.Label(stateRect, validationReport != null && validationReport.CanLaunch ? "ГОТОВ" : "НЕ ГОТОВ");
            GUI.color = prevColor;

            float y = header.yMax + 8f;
            if (validationReport == null)
            {
                Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "Нет данных проверки.");
                return;
            }

            for (int i = 0; i < validationReport.checks.Count; i++)
            {
                ShipValidationCheck check = validationReport.checks[i];
                Rect row = new Rect(inner.x, y, inner.width, 32f);
                string marker = check.passed ? "☑" : "☒";
                Color rowColor = check.passed ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f);

                GUI.color = rowColor;
                Widgets.Label(new Rect(row.x, row.y, 24f, 24f), marker);
                GUI.color = Color.white;

                string label = check.label;
                if (!string.IsNullOrEmpty(check.details))
                    label += " — " + check.details;

                Widgets.Label(new Rect(row.x + 24f, row.y, row.width - 24f, row.height), label);
                y += 28f;
            }

            if (validationReport.errors.Count > 0)
            {
                y += 4f;
                Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "Ошибок: " + validationReport.errors.Count);
            }
        }

        private void DrawDestinationList(Rect rect, OrbitalNode current)
        {
            Widgets.DrawMenuSection(rect);

            Rect inner = rect.ContractedBy(10f);
            Rect header = new Rect(inner.x, inner.y, inner.width, 25f);
            Widgets.Label(header, "Пункты назначения");

            Rect viewRect = new Rect(inner.x, header.yMax + 8f, inner.width - 16f, Mathf.Max(200f, Data.nodes.Count * 70f));
            Rect outRect = new Rect(inner.x, header.yMax + 8f, inner.width, inner.height - 35f);

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            float curY = viewRect.y;
            foreach (OrbitalNode node in Data.nodes)
            {
                Rect row = new Rect(viewRect.x, curY, viewRect.width, 60f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));

                Rect labelRect = new Rect(row.x + 10f, row.y + 8f, row.width - 160f, 24f);
                Widgets.Label(labelRect, Data.ResolveNodeLabel(node));

                float days = Mathf.Max(0.25f, OrbitalMath.Distance(current, node) / 45f);
                Rect timeRect = new Rect(row.x + 10f, row.y + 30f, row.width - 160f, 24f);
                Widgets.Label(timeRect, "Время полёта: " + days.ToString("0.0") + " д.");

                Rect buttonRect = new Rect(row.xMax - 130f, row.y + 15f, 120f, 30f);
                bool disabled = Data.IsShipTravelling(ship) || (current != null && current.id == node.id);

                if (disabled)
                    GUI.color = Color.gray;

                if (Widgets.ButtonText(buttonRect, "Лететь") && !disabled)
                {
                    RefreshValidationReport();
                    if (Data.StartTravel(ship, node))
                        Close();
                }

                GUI.color = Color.white;
                curY += 68f;
            }

            Widgets.EndScrollView();
        }
    }

    public class Window_ShipLanding : Window
    {
        private readonly ShipTransitRecord travel;
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(760f, 560f);

        public Window_ShipLanding(ShipTransitRecord travel)
        {
            this.travel = travel;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "Посадка корабля");

            List<Map> maps = Find.Maps.Where(m => m != null && m.IsPlayerHome).ToList();
            Rect outRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(100f, maps.Count * 64f));

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float curY = 0f;

            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                Rect row = new Rect(0f, curY, viewRect.width, 56f);
                Widgets.DrawMenuSection(row);
                Widgets.Label(new Rect(10f, row.y + 8f, row.width - 160f, 24f), map.Parent != null ? map.Parent.LabelCap : "Карта колонии");
                Widgets.Label(new Rect(10f, row.y + 28f, row.width - 160f, 24f), "Размер: " + map.Size.x + "x" + map.Size.z);

                if (Widgets.ButtonText(new Rect(row.width - 130f, row.y + 13f, 120f, 30f), "Посадить"))
                {
                    if (Find.World.GetComponent<WorldComponent_Interstellar>().TryLandShip(travel, map))
                        Close();
                }

                curY += 64f;
            }

            Widgets.EndScrollView();
        }
    }

    public class Window_TransitMonitor : Window
    {
        private readonly ShipTransitRecord record;
        private readonly WorldComponent_Interstellar data;

        public override Vector2 InitialSize => new Vector2(UI.screenWidth - 60f, UI.screenHeight - 80f);

        public Window_TransitMonitor(ShipTransitRecord record)
        {
            this.record = record;
            data = Find.World.GetComponent<WorldComponent_Interstellar>();
            forcePause = false;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            draggable = false;
            optionalTitle = "Монитор полёта";
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawSolarSystemUI(inRect, data, record, true);
        }

        public static void DrawSolarSystemUI(Rect inRect, WorldComponent_Interstellar data, ShipTransitRecord highlight, bool includeSidePanel)
        {
            Rect canvasRect = includeSidePanel ? new Rect(inRect.x, inRect.y, inRect.width * 0.72f, inRect.height) : inRect;
            Rect sideRect = includeSidePanel ? new Rect(canvasRect.xMax + 12f, inRect.y, inRect.width - canvasRect.width - 12f, inRect.height) : Rect.zero;

            Widgets.DrawMenuSection(canvasRect);
            Rect inner = canvasRect.ContractedBy(10f);

            GUI.BeginGroup(inner);
            Vector2 center = new Vector2(inner.width / 2f, inner.height / 2f);
            float scale = Mathf.Min(inner.width, inner.height) / 520f;

            foreach (OrbitalNode node in data.nodes)
                if (node.radius > 1f)
                    DrawOrbitRing(center, node.radius * scale);

            Widgets.DrawBoxSolid(new Rect(center.x - 18f, center.y - 18f, 36f, 36f), new Color(1f, 0.78f, 0.2f));

            foreach (OrbitalNode node in data.nodes)
                DrawNode(node, center, scale, data);

            foreach (ShipTransitRecord travel in data.activeTravels)
                DrawTravel(travel, center, scale, data, highlight);

            GUI.EndGroup();

            if (!includeSidePanel)
                return;

            Widgets.DrawMenuSection(sideRect);
            float curY = sideRect.y + 10f;
            Widgets.Label(new Rect(sideRect.x + 10f, curY, sideRect.width - 20f, 28f), "Маршруты");
            curY += 34f;

            foreach (ShipTransitRecord travel in data.activeTravels)
            {
                Rect row = new Rect(sideRect.x + 10f, curY, sideRect.width - 20f, 92f);
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));
                string src = data.ResolveNodeLabel(data.GetNodeById(travel.sourceId));
                string dst = data.ResolveNodeLabel(data.GetNodeById(travel.destinationId));
                Widgets.Label(new Rect(row.x + 8f, row.y + 6f, row.width - 16f, 24f), (travel.shipLabel ?? "Корабль") + ": " + src + " → " + dst);
                Widgets.Label(new Rect(row.x + 8f, row.y + 30f, row.width - 16f, 24f), "Прогресс: " + (travel.Progress * 100f).ToString("0") + "%");

                Rect progressRect = new Rect(row.x + 8f, row.y + 54f, row.width - 16f, 18f);
                Widgets.FillableBar(progressRect, travel.Progress);

                if (Widgets.ButtonText(new Rect(row.x + row.width - 118f, row.y + 72f, 110f, 22f), "Монитор"))
                    Find.WindowStack.Add(new Window_TransitMonitor(travel));

                if (travel.stage == InterstellarTransitStage.AwaitingLanding)
                {
                    if (Widgets.ButtonText(new Rect(row.x + 8f, row.y + 72f, 110f, 22f), "Посадка"))
                        Find.WindowStack.Add(new Window_ShipLanding(travel));
                }

                curY += 100f;
            }
        }

        private static void DrawOrbitRing(Vector2 center, float radius)
        {
            int segments = 72;
            Vector2 prev = center + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (360f / segments) * i * Mathf.Deg2Rad;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Widgets.DrawLine(prev, next, Color.gray, 1f);
                prev = next;
            }
        }

        private static void DrawNode(OrbitalNode node, Vector2 center, float scale, WorldComponent_Interstellar data)
        {
            Vector2 drawPos = center + OrbitalMath.Position(node) * scale;
            float size = 14f;
            Color color = Color.white;

            switch (node.type)
            {
                case OrbitalNodeType.Planet:
                    color = new Color(0.35f, 0.75f, 1f);
                    size = 18f;
                    break;
                case OrbitalNodeType.Station:
                    color = new Color(0.85f, 0.85f, 0.9f);
                    size = 12f;
                    break;
                default:
                    color = new Color(0.65f, 0.55f, 0.45f);
                    size = 12f;
                    break;
            }

            Widgets.DrawBoxSolid(new Rect(drawPos.x - size / 2f, drawPos.y - size / 2f, size, size), color);
            Widgets.Label(new Rect(drawPos.x + 10f, drawPos.y - 10f, 180f, 24f), data.ResolveNodeLabel(node));
        }

        private static void DrawTravel(ShipTransitRecord travel, Vector2 center, float scale, WorldComponent_Interstellar data, ShipTransitRecord highlight)
        {
            OrbitalNode src = data.GetNodeById(travel.sourceId);
            OrbitalNode dst = data.GetNodeById(travel.destinationId);
            if (src == null || dst == null)
                return;

            Vector2 a = center + OrbitalMath.Position(src) * scale;
            Vector2 b = center + OrbitalMath.Position(dst) * scale;
            Widgets.DrawLine(a, b, new Color(0.7f, 0.8f, 1f), highlight == travel ? 3f : 2f);

            Vector2 shipPos = Vector2.Lerp(a, b, travel.Progress);
            float size = highlight == travel ? 14f : 10f;
            Widgets.DrawBoxSolid(new Rect(shipPos.x - size / 2f, shipPos.y - size / 2f, size, size), Color.green);
        }
    }

    public class MainTabWindow_SolarSystem : MainTabWindow
    {
        public override Vector2 RequestedTabSize => new Vector2(UI.screenWidth - 40f, UI.screenHeight - 120f);

        public override void PreOpen()
        {
            base.PreOpen();
            Find.World.GetComponent<WorldComponent_Interstellar>().GenerateIfNeeded();
        }

        public override void DoWindowContents(Rect inRect)
        {
            WorldComponent_Interstellar data = Find.World.GetComponent<WorldComponent_Interstellar>();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, 400f, 32f), "Солнечная система");
            Text.Font = GameFont.Small;

            Rect body = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            Window_TransitMonitor.DrawSolarSystemUI(body, data, null, true);
        }
    }

    public class MainButtonWorker_InterstellarSystem : MainButtonWorker
    {
        public override void Activate()
        {
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("IO_SolarSystem");
            if (def == null)
            {
                Messages.Message("Не найден MainButtonDef IO_SolarSystem. Проверь Defs/MainButtons/IO_MainButtons.xml", MessageTypeDefOf.RejectInput, false);
                Log.Error("InterstellarOdyssey: MainButtonDef IO_SolarSystem not found.");
                return;
            }

            Find.MainTabsRoot.SetCurrentTab(def, true);
        }
    }
}
