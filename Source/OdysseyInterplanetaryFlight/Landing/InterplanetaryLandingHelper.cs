using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Генерирует карту на выбранном тайле — ванильный паттерн DLC Odyssey.
    ///
    /// Алгоритм (как в ванили при посадке гравикорабля):
    ///   1. Игрок выбрал тайл на глобусе через WorldTargeter
    ///   2. Создаём Settlement на этом тайле (он становится MapParent)
    ///   3. Устанавливаем фракцию игрока
    ///   4. Добавляем Settlement в WorldObjects
    ///   5. Генерируем Map через MapGenerator.GenerateMap (с LongEvent)
    ///   6. Возвращаем готовую карту для посадки корабля
    ///
    /// ВАЖНО: НЕ вызываем WorldGenerator.GenerateWorld — это ломает фракции.
    /// Каждая "новая планета" — это просто другой тайл на том же мировом шаре.
    /// </summary>
    public static class InterplanetaryLandingHelper
    {
        /// <summary>
        /// Создаёт Settlement на тайле и генерирует для него карту.
        /// Вызывается синхронно (не через LongEvent) — для совместимости с WorldTargeter callback.
        /// </summary>
        public static Map GenerateMapAtTile(int tile, OrbitalNode node)
        {
            if (tile < 0 || Find.WorldGrid == null || tile >= Find.WorldGrid.TilesCount)
            {
                Log.Warning("[IO:Landing] GenerateMapAtTile: невалидный тайл " + tile);
                return null;
            }

            // Если тайл занят — логируем и выходим (проверялось в canSelectTarget)
            if (Find.WorldObjects.AnyWorldObjectAt(tile))
            {
                Log.Warning("[IO:Landing] GenerateMapAtTile: тайл " + tile + " занят");
                return null;
            }

            try
            {
                // ── 1. Создаём Settlement как держатель карты ──────────────────
                Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(
                    WorldObjectDefOf.Settlement);

                settlement.Tile = tile;
                settlement.Name = BuildSettlementName(node);

                Faction playerFaction = Find.FactionManager?.OfPlayer
                    ?? Find.FactionManager?.AllFactionsListForReading?.FirstOrDefault(f => f != null && f.IsPlayer);

                if (playerFaction == null)
                {
                    Log.Error("[IO:Landing] GenerateMapAtTile: player faction not found.");
                    return null;
                }

                settlement.SetFaction(playerFaction);

                Find.WorldObjects.Add(settlement);

                // ── 2. Определяем параметры карты ─────────────────────────────
                IntVec3        mapSize = GetMapSize(node);
                MapGeneratorDef genDef = GetMapGeneratorDef(tile, node);

                if (genDef == null)
                {
                    Log.Error("[IO:Landing] GenerateMapAtTile: не найден MapGeneratorDef");
                    Find.WorldObjects.Remove(settlement);
                    return null;
                }

                // ── 3. Генерируем карту — тот же вызов что и в ванили ─────────
                Map map = MapGenerator.GenerateMap(mapSize, settlement, genDef);

                if (map == null)
                {
                    Log.Warning("[IO:Landing] GenerateMapAtTile: MapGenerator вернул null");
                    Find.WorldObjects.Remove(settlement);
                    return null;
                }

                // ── 3а. Перепривязываем Settlement к актуальной фракции игрока ─
                // После PrepareNewPlanetWorld → GenerateWorld FactionManager обновился.
                // Settlement мог получить ссылку на старый объект Faction — явно синхронизируем.
                Faction currentPlayerFaction = null;
                try { currentPlayerFaction = Find.FactionManager?.OfPlayer; } catch { }
                currentPlayerFaction = currentPlayerFaction
                    ?? Find.FactionManager?.AllFactionsListForReading?.FirstOrDefault(f => f != null && f.IsPlayer);

                if (currentPlayerFaction != null && settlement.Faction != currentPlayerFaction)
                {
                    settlement.SetFaction(currentPlayerFaction);
                    Log.Message("[IO:Landing] Settlement faction re-synced to: " + currentPlayerFaction.Name);
                }

                // ── 4. Помечаем узел — запоминаем тайл для повторных визитов ──
                if (node != null)
                {
                    node.generatedTile             = tile;
                    node.generatedWorldObjectLabel = settlement.Name;
                }

                Log.Message("[IO:Landing] Карта сгенерирована: tile=" + tile
                    + " size=" + mapSize + " genDef=" + genDef.defName
                    + " settlement=" + settlement.Name);

                return map;
            }
            catch (Exception ex)
            {
                Log.Error("[IO:Landing] GenerateMapAtTile failed: " + ex);
                // Убираем созданный Settlement при ошибке
                WorldObject orphan = Find.WorldObjects.WorldObjectAt(tile, WorldObjectDefOf.Settlement);
                if (orphan != null) Find.WorldObjects.Remove(orphan);
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Вспомогательные
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildSettlementName(OrbitalNode node)
        {
            if (node != null && !string.IsNullOrEmpty(node.label))
                return node.label;

            // Генерируем имя через RulePack фракции игрока
            try
            {
                Faction player = Faction.OfPlayer;
                if (player?.def?.settlementNameMaker != null)
                    return NameGenerator.GenerateName(player.def.settlementNameMaker);
            }
            catch { }

            // Fallback
            return "Колония";
        }

        private static IntVec3 GetMapSize(OrbitalNode node)
        {
            if (node == null) return new IntVec3(250, 1, 250);

            switch (node.type)
            {
                case OrbitalNodeType.Station:     return new IntVec3(150, 1, 150);
                case OrbitalNodeType.Asteroid:
                case OrbitalNodeType.AsteroidBelt:return new IntVec3(180, 1, 180);
                default:                           return new IntVec3(250, 1, 250);
            }
        }

        /// <summary>
        /// Выбирает подходящий MapGeneratorDef для тайла и типа узла.
        /// Приоритет: Base_Player → Base_Faction → первый доступный.
        /// </summary>
        private static MapGeneratorDef GetMapGeneratorDef(int tile, OrbitalNode node)
        {
            // Для станций — базовый генератор (нет открытой местности)
            if (node?.type == OrbitalNodeType.Station)
                return DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Player")
                    ?? DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();

            // Для планет — биомный генератор (Base_Player учитывает биом тайла)
            return DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Player")
                ?? DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Faction")
                ?? DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();
        }
    }
}
