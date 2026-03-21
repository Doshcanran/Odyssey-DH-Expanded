using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Управляет архивированием и восстановлением поселений игрока
    /// при межпланетных перелётах.
    ///
    /// Принцип работы:
    ///   • При старте с планеты A → все поселения игрока на тайлах планеты A
    ///     удаляются с глобальной карты мира и сохраняются в архиве.
    ///   • При посадке на планету B → если в архиве есть поселения для B,
    ///     они восстанавливаются.
    ///   • Пока поселение в архиве — оно полностью недоступно (нельзя ни атаковать,
    ///     ни взаимодействовать, ни получать квесты).
    ///
    /// ВАЖНО: В RimWorld планета одна и та же (тайловый шар), поэтому «смена планеты»
    /// реализована как перегенерация WorldInfo параметров и пересохранение сетлментов
    /// в архив по ключу nodeId. При возврате на ту же планету (тот же nodeId) всё
    /// восстанавливается точно так же.
    /// </summary>
    public static class PlanetArchiveUtility
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Подготовка архивов для всех планет при генерации мира
        // ─────────────────────────────────────────────────────────────────────

        public static void PrepareArchivesForAllPlanets(GalaxyWorldConfiguration config)
        {
            if (config?.galaxies == null) return;

            foreach (GalaxyDefinition galaxy in config.galaxies)
            {
                if (galaxy?.planets == null) continue;

                foreach (PlanetDefinition_IO planet in galaxy.planets)
                {
                    if (planet == null) continue;

                    if (planet.archive == null)
                        planet.archive = new PlanetArchiveData();

                    planet.archive.seed         = GeneratePlanetSeed(galaxy.id, planet.id, planet.seedOffset);
                    planet.archive.generated    = true;
                    planet.archive.generatedLabel  = planet.label;
                    planet.archive.cachedCoverage  = planet.coverage;
                    planet.archive.cachedRainfall  = planet.overallRainfall;
                    planet.archive.cachedTemperature = planet.overallTemperature;
                    planet.archive.cachedPopulation = planet.overallPopulation;
                    planet.archive.cachedPollution  = planet.pollution;
                    planet.archive.cachedWorldTileCount =
                        Find.WorldGrid != null ? Find.WorldGrid.TilesCount : 0;
                }
            }
        }

        private static string GeneratePlanetSeed(string galaxyId, string planetId, int seedOffset)
        {
            string baseSeed = Find.World?.info?.seedString ?? "interstellar_odyssey";
            return baseSeed + "_" + galaxyId + "_" + planetId + "_" + seedOffset;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Архивирование поселений при взлёте с планеты
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Вызывается в <see cref="WorldComponent_Interstellar.StartTravel"/> перед стартом.
        /// Убирает с глобальной карты все поселения игрока на текущей планете
        /// и сохраняет их в <paramref name="archive"/> по ключу <paramref name="sourcePlanetNodeId"/>.
        /// </summary>
        public static void ArchiveSettlementsForDeparture(
            string sourcePlanetNodeId,
            ref Dictionary<string, List<SettlementArchiveEntry>> archive)
        {
            if (string.IsNullOrEmpty(sourcePlanetNodeId)) return;
            if (archive == null)
                archive = new Dictionary<string, List<SettlementArchiveEntry>>();

            if (!archive.ContainsKey(sourcePlanetNodeId))
                archive[sourcePlanetNodeId] = new List<SettlementArchiveEntry>();

            List<Settlement> playerSettlements = Find.WorldObjects.Settlements
                .Where(s => s != null && s.Faction != null && s.Faction.IsPlayer)
                .ToList();

            if (playerSettlements.Count == 0) return;

            int archived = 0;
            foreach (Settlement settlement in playerSettlements)
            {
                SettlementArchiveEntry entry = new SettlementArchiveEntry
                {
                    tile           = settlement.Tile,
                    name           = settlement.Name,
                    planetNodeId   = sourcePlanetNodeId
                };
                archive[sourcePlanetNodeId].Add(entry);

                // Удаляем с мировой карты — поселение «заморожено»
                // Карта (Map) при этом остаётся в памяти, к ней можно вернуться
                Find.WorldObjects.Remove(settlement);
                archived++;

                Log.Message("[IO:PlanetArchive] Заархивировано поселение «"
                    + entry.name + "» (тайл " + entry.tile + ") для планеты " + sourcePlanetNodeId);
            }

            if (archived > 0)
            {
                Messages.Message(
                    "Поселения на предыдущей планете заморожены (" + archived + " шт.). "
                    + "Они недоступны до возврата.",
                    MessageTypeDefOf.CautionInput, false);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Восстановление поселений при посадке на планету
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Вызывается в <see cref="WorldComponent_Interstellar.TryLandShip"/> после посадки.
        /// Восстанавливает заархивированные поселения для <paramref name="destPlanetNodeId"/>.
        /// </summary>
        public static void RestoreSettlementsOnArrival(
            string destPlanetNodeId,
            ref Dictionary<string, List<SettlementArchiveEntry>> archive)
        {
            if (string.IsNullOrEmpty(destPlanetNodeId)) return;
            if (archive == null || !archive.ContainsKey(destPlanetNodeId)) return;

            List<SettlementArchiveEntry> entries = archive[destPlanetNodeId];
            if (entries == null || entries.Count == 0) return;

            Faction playerFaction = Find.FactionManager?.OfPlayer;
            if (playerFaction == null) return;

            int restored = 0;
            foreach (SettlementArchiveEntry entry in entries)
            {
                if (Find.WorldObjects.AnyWorldObjectAt(entry.tile))
                {
                    Log.Warning("[IO:PlanetArchive] Тайл " + entry.tile + " занят, «"
                        + entry.name + "» не восстановлено.");
                    continue;
                }

                Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(
                    WorldObjectDefOf.Settlement);
                settlement.Tile = entry.tile;
                settlement.Name = entry.name ?? "Поселение";
                settlement.SetFaction(playerFaction);
                Find.WorldObjects.Add(settlement);
                restored++;

                Log.Message("[IO:PlanetArchive] Восстановлено поселение «"
                    + settlement.Name + "» (тайл " + entry.tile + ")");
            }

            entries.Clear();

            if (restored > 0)
            {
                Messages.Message(
                    "Поселения восстановлены на этой планете: " + restored + " шт.",
                    MessageTypeDefOf.PositiveEvent, false);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Применение параметров новой планеты к WorldInfo
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Применяет климатические параметры планеты к WorldInfo.
        /// Влияет на спавн событий и NPC, но не перегенерирует тайлы.
        /// </summary>
        public static void ApplyPlanetParamsToWorldInfo(PlanetDefinition_IO planetDef)
        {
            if (planetDef == null || Find.World?.info == null) return;
            if (planetDef.useVanillaDefaults) return;

            WorldInfo info = Find.World.info;

            // RimWorld 1.6: глобальные overalls живут в WorldInfo как поля
            // Проверяем через reflection, поскольку API мог измениться
            try
            {
                var field = typeof(WorldInfo).GetField("overallRainfall",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    field.SetValue(info, planetDef.overallRainfall);

                field = typeof(WorldInfo).GetField("overallTemperature",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    field.SetValue(info, planetDef.overallTemperature);

                field = typeof(WorldInfo).GetField("overallPopulation",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    field.SetValue(info, planetDef.overallPopulation);

                field = typeof(WorldInfo).GetField("pollution",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    field.SetValue(info, planetDef.pollution);

                // coverage (процент суши) не меняем на лету — только при генерации мира
                Log.Message("[IO:PlanetArchive] Параметры планеты «" + planetDef.label
                    + "» применены: T=" + planetDef.overallTemperature
                    + " R=" + planetDef.overallRainfall
                    + " Pop=" + planetDef.overallPopulation
                    + " Pol=" + planetDef.pollution);
            }
            catch (Exception ex)
            {
                Log.Warning("[IO:PlanetArchive] ApplyPlanetParamsToWorldInfo: " + ex.Message);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Модель записи архива поселения
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Данные о заархивированном поселении (убранном с глобальной карты мира).
    /// </summary>
    public class SettlementArchiveEntry : IExposable
    {
        /// <summary>Тайл мирового объекта</summary>
        public int tile;

        /// <summary>Название поселения</summary>
        public string name;

        /// <summary>ID узла OrbitalNode планеты, на которой осталось поселение</summary>
        public string planetNodeId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tile,         "tile",         0);
            Scribe_Values.Look(ref name,         "name");
            Scribe_Values.Look(ref planetNodeId, "planetNodeId");
        }
    }
}
