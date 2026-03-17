
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

            foreach (Thing thing in console.Map.listerThings.AllThings)
            {
                if (thing == null || thing.Destroyed || !thing.Spawned || thing.def == null)
                    continue;

                if (thing.Faction != console.Faction)
                    continue;

                string defName = thing.def.defName ?? string.Empty;
                string label = thing.def.label ?? string.Empty;

                if (defName.IndexOf("grav", StringComparison.OrdinalIgnoreCase) >= 0
                    || defName.IndexOf("ship", StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("грав", StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("кораб", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (ShipCaptureUtility.IsStructuralShipThing(thing))
                        return thing;
                }
            }

            return null;
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

            if (buildings == null) buildings = new List<ShipThingSnapshot>();
            if (items == null) items = new List<ShipThingSnapshot>();
            if (pawns == null) pawns = new List<ShipPawnSnapshot>();
            if (terrains == null) terrains = new List<ShipTerrainSnapshot>();
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
            "Substructure"
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
            if (!IsShipFloor(terrain))
                return false;

            string defName = (terrain.defName ?? string.Empty).ToLowerInvariant();
            string label = (terrain.label ?? string.Empty).ToLowerInvariant();

            if (defName.Contains("substructure") || label.Contains("основан"))
                return false;

            return true;
        }
    }

    public static class ShipCaptureUtility
    {
        private static readonly HashSet<string> StructuralDefs = new HashSet<string>
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

        public static bool TryCaptureAndDespawnShip(Thing shipAnchor, string currentNodeId, out ShipSnapshot snapshot)
        {
            snapshot = null;

            if (shipAnchor == null || shipAnchor.Map == null || shipAnchor.def == null)
                return false;

            Map map = shipAnchor.Map;
            Faction faction = shipAnchor.Faction;
            IntVec3 anchorCell = shipAnchor.Position;

            List<Thing> structural = CollectStructuralShipThings(map, faction);
            if (structural.Count == 0)
                return false;

            HashSet<IntVec3> terrainCells = CollectConnectedShipTerrain(map, anchorCell);
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

            CaptureTerrain(map, anchorCell, terrainCells, snapshot);

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

                if (!shipBounds.Contains(thing.Position))
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

            DespawnCapturedThings(snapshot);
            RemoveCapturedTerrain(map, capturedCells);
            CleanupResidualShipBuildings(map, capturedCells);

            Log.Message("[InterstellarOdyssey] Ship captured. Buildings=" + snapshot.buildings.Count
                + " Items=" + snapshot.items.Count
                + " Pawns=" + snapshot.pawns.Count
                + " Terrains=" + snapshot.terrains.Count);

            return true;
        }

        public static bool IsStructuralShipThing(Thing thing)
        {
            if (thing == null || thing.def == null)
                return false;

            if (thing.def.category != ThingCategory.Building)
                return false;

            if (StructuralDefs.Contains(thing.def.defName))
                return true;

            string defName = (thing.def.defName ?? string.Empty).ToLowerInvariant();
            string label = (thing.def.label ?? string.Empty).ToLowerInvariant();
            string className = thing.GetType().FullName != null ? thing.GetType().FullName.ToLowerInvariant() : string.Empty;

            if (defName.Contains("grav")
                || defName.Contains("ship")
                || defName.Contains("thruster")
                || defName.Contains("hull")
                || defName.Contains("bridge")
                || defName.Contains("console")
                || defName.Contains("bulkhead")
                || defName.Contains("superstructure")
                || label.Contains("грав")
                || label.Contains("кораб")
                || label.Contains("палуб")
                || label.Contains("надстрой")
                || className.Contains("grav")
                || className.Contains("ship"))
            {
                return true;
            }

            return false;
        }

        private static List<Thing> CollectStructuralShipThings(Map map, Faction faction)
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

                if (IsStructuralShipThing(thing))
                {
                    result.Add(thing);
                    continue;
                }

                CellRect rect = thing.OccupiedRect();
                foreach (IntVec3 cell in rect.Cells)
                {
                    if (ShipFloorUtility.IsShipFloorCell(map, cell))
                    {
                        result.Add(thing);
                        break;
                    }
                }
            }

            return result;
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

        private static void CaptureTerrain(Map map, IntVec3 anchorCell, HashSet<IntVec3> terrainCells, ShipSnapshot snapshot)
        {
            foreach (IntVec3 cell in terrainCells)
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

        private static void RemoveCapturedTerrain(Map map, HashSet<IntVec3> capturedCells)
        {
            if (map == null || capturedCells == null)
                return;

            TerrainDef natural = TerrainDefOf.Soil ?? TerrainDefOf.Sand;

            foreach (IntVec3 cell in capturedCells)
            {
                if (!cell.InBounds(map))
                    continue;

                if (map.roofGrid != null)
                    map.roofGrid.SetRoof(cell, null);

                TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                if (ShipFloorUtility.IsShipFloor(terrain))
                {
                    map.terrainGrid.SetTerrain(cell, natural);
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Terrain);
                }
            }
        }

        private static void CleanupResidualShipBuildings(Map map, HashSet<IntVec3> capturedCells)
        {
            if (map == null || capturedCells == null)
                return;

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

                    if (IsResidualShipPart(thing))
                        thing.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private static bool IsResidualShipPart(Thing thing)
        {
            if (thing == null || thing.def == null)
                return false;

            string defName = (thing.def.defName ?? string.Empty).ToLowerInvariant();
            string label = (thing.def.label ?? string.Empty).ToLowerInvariant();
            string className = thing.GetType().FullName != null ? thing.GetType().FullName.ToLowerInvariant() : string.Empty;

            if (thing.def.category == ThingCategory.Building)
            {
                if (defName.Contains("grav")
                    || defName.Contains("gravship")
                    || defName.Contains("ship")
                    || defName.Contains("hull")
                    || defName.Contains("deck")
                    || defName.Contains("floor")
                    || defName.Contains("plating")
                    || defName.Contains("thruster")
                    || defName.Contains("console")
                    || defName.Contains("bridge")
                    || defName.Contains("superstructure")
                    || defName.Contains("bulkhead")
                    || defName.Contains("chemfueltank")
                    || defName.Contains("gravcore")
                    || label.Contains("grav")
                    || label.Contains("грав")
                    || label.Contains("кораб")
                    || label.Contains("палуб")
                    || label.Contains("надстрой")
                    || label.Contains("обшив")
                    || label.Contains("двигатель")
                    || label.Contains("мостик"))
                {
                    return true;
                }
            }

            if (thing is Blueprint || thing is Frame)
            {
                if (defName.Contains("grav")
                    || defName.Contains("ship")
                    || defName.Contains("superstructure")
                    || label.Contains("кораб")
                    || label.Contains("палуб")
                    || label.Contains("надстрой"))
                {
                    return true;
                }
            }

            if (className.Contains("grav") || className.Contains("ship"))
                return true;

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

                IntVec3 cell = targetCenter + entry.offset;
                if (!cell.InBounds(map))
                    continue;

                Thing thing = entry.thing;
                thing.Position = cell;

                if (!thing.Spawned)
                    GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
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

                IntVec3 cell = targetCenter + entry.offset;
                if (!cell.InBounds(map))
                    continue;

                Pawn pawn = entry.pawn;
                if (!pawn.Spawned)
                    GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
            }
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
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "Орбитальная карта");
            Text.Font = GameFont.Small;

            Rect topInfo = new Rect(inRect.x, titleRect.yMax + 10f, inRect.width, 30f);
            OrbitalNode current = Data.GetCurrentNodeForShip(ship);
            Widgets.Label(topInfo, "Текущая цель: " + Data.ResolveNodeLabel(current));

            Rect leftRect = new Rect(inRect.x, topInfo.yMax + 10f, inRect.width * 0.58f, inRect.height - 95f);
            Rect rightRect = new Rect(leftRect.xMax + 12f, topInfo.yMax + 10f, inRect.width - leftRect.width - 12f, inRect.height - 95f);

            DrawOrbitCanvas(leftRect);
            DrawDestinationList(rightRect, current);

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
