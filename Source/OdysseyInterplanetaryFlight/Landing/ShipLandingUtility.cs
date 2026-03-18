using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public static class ShipLandingUtility
        {
            public static bool TryRestoreShip(ShipSnapshot snapshot, Map map, IntVec3 targetCenter, ShipLandingMode mode, out Thing restoredAnchor)
            {
                restoredAnchor = null;

                if (snapshot == null || map == null)
                    return false;

                RestoreTerrain(snapshot, map, targetCenter);
                RestoreBuildings(snapshot, map, targetCenter, ref restoredAnchor);
                RestoreRoofs(snapshot, map, targetCenter);
                RestoreItems(snapshot, map, targetCenter, mode);
                RestorePawns(snapshot, map, targetCenter, mode);
                ApplyLandingModePostEffects(snapshot, map, targetCenter, mode);

                return true;
            }

            public static bool TryFindLandingCenter(ShipSnapshot snapshot, Map map, ShipLandingMode mode, out IntVec3 center)
            {
                center = IntVec3.Invalid;
                if (snapshot == null || map == null)
                    return false;

                switch (mode)
                {
                    case ShipLandingMode.StationDocking:
                        center = map.Center;
                        return true;
                    case ShipLandingMode.Emergency:
                        center = CellFinderLoose.RandomCellWith(c => c.InBounds(map) && c.Standable(map), map, 5000);
                        break;
                    case ShipLandingMode.OrbitalDrop:
                        center = CellFinderLoose.RandomCellWith(c => c.InBounds(map) && c.Standable(map), map, 3500);
                        break;
                    case ShipLandingMode.UnpreparedSurface:
                        center = CellFinderLoose.RandomCellWith(c => c.InBounds(map) && c.Walkable(map), map, 6000);
                        break;
                    default:
                        center = CellFinderLoose.RandomCellWith(c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map), map, 2000);
                        break;
                }

                if (!center.IsValid)
                    center = map.Center;

                return center.IsValid;
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

            private static void RestoreItems(ShipSnapshot snapshot, Map map, IntVec3 targetCenter, ShipLandingMode mode)
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
                    if (mode == ShipLandingMode.OrbitalDrop || mode == ShipLandingMode.Emergency)
                        desiredCell += new IntVec3(Rand.RangeInclusive(-2, 2), 0, Rand.RangeInclusive(-2, 2));
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

            private static void RestorePawns(ShipSnapshot snapshot, Map map, IntVec3 targetCenter, ShipLandingMode mode)
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
                    if (mode == ShipLandingMode.OrbitalDrop || mode == ShipLandingMode.Emergency)
                        desiredCell += new IntVec3(Rand.RangeInclusive(-3, 3), 0, Rand.RangeInclusive(-3, 3));
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


            private static void ApplyLandingModePostEffects(ShipSnapshot snapshot, Map map, IntVec3 targetCenter, ShipLandingMode mode)
            {
                if (mode == ShipLandingMode.Emergency && snapshot != null && snapshot.buildings != null)
                {
                    for (int i = 0; i < snapshot.buildings.Count; i++)
                    {
                        Thing building = snapshot.buildings[i]?.thing;
                        if (building == null || !building.Spawned || building.Destroyed)
                            continue;

                        if (Rand.Chance(0.14f))
                            building.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Rand.RangeInclusive(2, 10)));
                    }
                }

                if (mode == ShipLandingMode.OrbitalDrop)
                    Messages.Message("Корабль завершил орбитальный дроп. Экипаж и груз рассредоточены вокруг точки входа.", MessageTypeDefOf.NeutralEvent, false);
                else if (mode == ShipLandingMode.UnpreparedSurface)
                    Messages.Message("Посадка выполнена на неподготовленную поверхность.", MessageTypeDefOf.NeutralEvent, false);
                else if (mode == ShipLandingMode.StationDocking)
                    Messages.Message("Корабль успешно состыковался со станцией.", MessageTypeDefOf.PositiveEvent, false);
                else if (mode == ShipLandingMode.Emergency)
                    Messages.Message("Выполнена аварийная посадка. Возможны повреждения корпуса.", MessageTypeDefOf.ThreatSmall, false);
            }

            public static void SpawnTransitLoot(ShipTransitRecord record, Map map, IntVec3 center)
            {
                if (record == null || map == null)
                    return;

                if (record.salvageSteel > 0)
                    SpawnLootStack(ThingDefOf.Steel, record.salvageSteel, map, center);

                ThingDef componentDef = DefDatabase<ThingDef>.GetNamedSilentFail("ComponentIndustrial");
                if (componentDef != null && record.salvageComponents > 0)
                    SpawnLootStack(componentDef, record.salvageComponents, map, center);

                record.salvageSteel = 0;
                record.salvageComponents = 0;
            }

            private static void SpawnLootStack(ThingDef def, int count, Map map, IntVec3 center)
            {
                if (def == null || count <= 0 || map == null)
                    return;

                int remaining = count;
                while (remaining > 0)
                {
                    Thing stack = ThingMaker.MakeThing(def);
                    int stackCount = Mathf.Min(remaining, def.stackLimit > 0 ? def.stackLimit : remaining);
                    stack.stackCount = stackCount;

                    if (TryFindRestoreCellForThing(map, center + new IntVec3(Rand.RangeInclusive(-4, 4), 0, Rand.RangeInclusive(-4, 4)), out IntVec3 cell))
                        GenSpawn.Spawn(stack, cell, map, WipeMode.Vanish);

                    remaining -= stackCount;
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
}
