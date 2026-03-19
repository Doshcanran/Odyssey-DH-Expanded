using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Создаёт, хранит и уничтожает карту вакуума для корабля в полёте.
    /// </summary>
    public static class VoidMapUtility
    {
        // Передаётся в GenStep через статическое поле до вызова GenerateMap
        public static ShipSnapshot PendingSnapshot;

        // ─────────────────────────────────────────────────────────────────────
        // Создание карты
        // ─────────────────────────────────────────────────────────────────────

        public static bool CreateVoidMap(ShipTransitRecord record)
        {
            if (record == null || record.snapshot == null)
            {
                Log.Warning("[InterstellarOdyssey] VoidMapUtility.CreateVoidMap: нет записи или снапшота.");
                return false;
            }

            try
            {
                int tile = FindFreeTileForVoidMap(record.shipThingId);
                if (tile < 0)
                {
                    Log.Warning("[InterstellarOdyssey] VoidMapUtility.CreateVoidMap: не удалось найти тайл.");
                    return false;
                }

                WorldObjectDef woDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("IO_ShipInTransit")
                                       ?? WorldObjectDefOf.Settlement;

                WorldObject_ShipInTransit worldObj = (WorldObject_ShipInTransit)WorldObjectMaker.MakeWorldObject(woDef);
                worldObj.Tile = tile;
                worldObj.transitShipThingId = record.shipThingId;
                worldObj.SetFaction(Faction.OfPlayer);
                Find.WorldObjects.Add(worldObj);

                PendingSnapshot = record.snapshot;

                IntVec3 mapSize = ComputeMapSizeForSnapshot(record.snapshot);

                MapGeneratorDef genDef = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("IO_VoidMapGenerator");
                if (genDef == null)
                {
                    Log.Error("[InterstellarOdyssey] VoidMapUtility: MapGeneratorDef IO_VoidMapGenerator не найден!");
                    PendingSnapshot = null;
                    Find.WorldObjects.Remove(worldObj);
                    return false;
                }

                Map voidMap = MapGenerator.GenerateMap(mapSize, worldObj, genDef);
                PendingSnapshot = null;

                record.voidMapTile = tile;

                Log.Message("[InterstellarOdyssey] Карта вакуума создана. tile=" + tile + " size=" + mapSize);
                return true;
            }
            catch (Exception ex)
            {
                PendingSnapshot = null;
                Log.Error("[InterstellarOdyssey] VoidMapUtility.CreateVoidMap failed: " + ex);
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Захват корабля с карты вакуума перед посадкой
        // ─────────────────────────────────────────────────────────────────────

        public static bool RecaptureShipFromVoidMap(ShipTransitRecord record)
        {
            if (record == null || record.voidMapTile < 0)
                return false;

            Map voidMap = GetVoidMap(record.voidMapTile);
            if (voidMap == null)
            {
                Log.Warning("[InterstellarOdyssey] RecaptureShipFromVoidMap: карта не найдена (tile=" + record.voidMapTile + ").");
                return false;
            }

            Thing anchor = FindShipAnchorOnMap(voidMap, record);
            if (anchor == null)
            {
                Log.Warning("[InterstellarOdyssey] RecaptureShipFromVoidMap: якорь не найден на карте вакуума.");
                return false;
            }

            if (!ShipCaptureUtility.TryCaptureAndDespawnShip(anchor, record.sourceId, out ShipSnapshot fresh))
            {
                Log.Warning("[InterstellarOdyssey] RecaptureShipFromVoidMap: не удалось захватить корабль.");
                return false;
            }

            // ВАЖНО: TryCaptureAndDespawnShip собирает только пешек ВНУТРИ корабля.
            // На карте вакуума пешки могли выйти за борт — собираем их отдельно.
            AddRemainingPlayerPawnsToSnapshot(voidMap, fresh);

            record.snapshot = fresh;
            Log.Message("[InterstellarOdyssey] Корабль захвачен с карты вакуума. Pawns=" + fresh.pawns.Count);
            return true;
        }

        /// <summary>
        /// Добавляет в снапшот всех пешек игрока с карты вакуума,
        /// которые ещё не были захвачены (т.е. ещё заспавнены).
        /// </summary>
        private static void AddRemainingPlayerPawnsToSnapshot(Map voidMap, ShipSnapshot snapshot)
        {
            if (voidMap == null || snapshot == null)
                return;

            // Собираем thingID уже захваченных пешек
            var capturedIds = new System.Collections.Generic.HashSet<int>();
            foreach (var ps in snapshot.pawns)
                if (ps?.pawn != null)
                    capturedIds.Add(ps.pawn.thingIDNumber);

            IntVec3 anchor = snapshot.anchorCell;

            foreach (Pawn pawn in voidMap.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == null || pawn.Destroyed || !pawn.Spawned)
                    continue;

                if (pawn.Faction != Faction.OfPlayer)
                    continue;

                if (capturedIds.Contains(pawn.thingIDNumber))
                    continue;

                // Добавляем пешку в снапшот и деспавним
                snapshot.pawns.Add(new ShipPawnSnapshot
                {
                    pawn = pawn,
                    offset = pawn.Position - anchor
                });

                pawn.DeSpawn();
                Log.Message("[InterstellarOdyssey] Дополнительно захвачена пешка: " + pawn.LabelCap);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Удаление карты
        // ─────────────────────────────────────────────────────────────────────

        public static void DestroyVoidMap(ShipTransitRecord record)
        {
            if (record == null || record.voidMapTile < 0)
                return;

            try
            {
                Map voidMap = GetVoidMap(record.voidMapTile);
                if (voidMap != null)
                {
                    foreach (Pawn pawn in voidMap.mapPawns.AllPawnsSpawned.ToList())
                        if (pawn?.Faction == Faction.OfPlayer)
                            pawn.DeSpawn();

                    Current.Game.DeinitAndRemoveMap(voidMap, false);
                }

                WorldObject_ShipInTransit wo = Find.WorldObjects.AllWorldObjects
                    .OfType<WorldObject_ShipInTransit>()
                    .FirstOrDefault(o => o.transitShipThingId == record.shipThingId);

                if (wo != null)
                    Find.WorldObjects.Remove(wo);

                record.voidMapTile = -1;
                Log.Message("[InterstellarOdyssey] Карта вакуума удалена.");
            }
            catch (Exception ex)
            {
                Log.Error("[InterstellarOdyssey] VoidMapUtility.DestroyVoidMap failed: " + ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Вспомогательные
        // ─────────────────────────────────────────────────────────────────────

        public static Map GetVoidMap(int tile)
        {
            if (tile < 0) return null;
            return Find.Maps.FirstOrDefault(m => m?.Parent != null && m.Parent.Tile == tile);
        }

        public static bool HasVoidMap(ShipTransitRecord record)
        {
            return record != null && record.voidMapTile >= 0 && GetVoidMap(record.voidMapTile) != null;
        }

        private static Thing FindShipAnchorOnMap(Map map, ShipTransitRecord record)
        {
            if (map == null) return null;

            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t == null || t.Destroyed || !t.Spawned) continue;
                if (t.thingIDNumber == record.shipThingId) return t;
            }

            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t == null || t.Destroyed || !t.Spawned) continue;
                if (ShipPartUtility.IsCore(t) || ShipPartUtility.IsNavigationConsole(t)) return t;
            }

            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t == null || t.Destroyed || !t.Spawned) continue;
                if (ShipPartUtility.IsShipStructure(t)) return t;
            }

            return null;
        }

        private static IntVec3 ComputeMapSizeForSnapshot(ShipSnapshot snapshot)
        {
            if (snapshot == null) return new IntVec3(150, 1, 150);

            int minX = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxZ = int.MinValue;
            bool hasAny = false;

            foreach (ShipTerrainSnapshot t in snapshot.terrains)
            {
                hasAny = true;
                minX = Mathf.Min(minX, t.offset.x); minZ = Mathf.Min(minZ, t.offset.z);
                maxX = Mathf.Max(maxX, t.offset.x); maxZ = Mathf.Max(maxZ, t.offset.z);
            }

            foreach (ShipThingSnapshot b in snapshot.buildings)
            {
                hasAny = true;
                minX = Mathf.Min(minX, b.offset.x); minZ = Mathf.Min(minZ, b.offset.z);
                maxX = Mathf.Max(maxX, b.offset.x); maxZ = Mathf.Max(maxZ, b.offset.z);
            }

            if (!hasAny) return new IntVec3(150, 1, 150);

            int padding = 50;
            int sizeX = Mathf.Max(150, (maxX - minX + 1) + padding * 2);
            int sizeZ = Mathf.Max(150, (maxZ - minZ + 1) + padding * 2);
            if (sizeX % 2 != 0) sizeX++;
            if (sizeZ % 2 != 0) sizeZ++;

            return new IntVec3(sizeX, 1, sizeZ);
        }

        private static int FindFreeTileForVoidMap(int shipThingId)
        {
            int tileCount = Find.WorldGrid.TilesCount;
            if (tileCount <= 0) return -1;

            int seed = Mathf.Abs(shipThingId * 1234567 + 7654321);
            for (int attempt = 0; attempt < 2000; attempt++)
            {
                int candidate = ((seed + attempt * 997) % tileCount + tileCount) % tileCount;
                if (Find.WorldObjects.AllWorldObjects.Any(o => o != null && o.Tile == candidate)) continue;
                if (Find.WorldGrid[candidate] == null) continue;
                return candidate;
            }

            return -1;
        }
    }
}
