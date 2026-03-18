using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
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
}
