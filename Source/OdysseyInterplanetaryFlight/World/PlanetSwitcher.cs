using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Реализует смену планеты при межпланетной посадке.
    ///
    /// Алгоритм:
    ///   1. Сохраняем объект Faction игрока и все его relations ДО GenerateWorld
    ///   2. Вызываем WorldGenerator.GenerateWorld — мир полностью перегенерируется
    ///   3. Находим новую "player faction" в новом FactionManager
    ///   4. Через reflection заменяем её нашей сохранённой faction
    ///   5. Восстанавливаем relations
    ///
    /// Это даёт визуально новый мировой шар (новые биомы, рельеф)
    /// при этом пешки и постройки сохраняют корректную faction.
    /// </summary>
    public static class PlanetSwitcher
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Состояние планеты
        // ─────────────────────────────────────────────────────────────────────

        public class PlanetState : IExposable
        {
            public string nodeId;
            public string seed;
            public float overallRainfall    = 1f;
            public float overallTemperature = 1f;
            public float overallPopulation  = 1f;
            public float pollution          = 0f;
            public float coverage           = 0.3f;
            public List<SettlementSaveData> settlements = new List<SettlementSaveData>();

            public void ExposeData()
            {
                Scribe_Values.Look(ref nodeId,             "nodeId");
                Scribe_Values.Look(ref seed,               "seed");
                Scribe_Values.Look(ref overallRainfall,    "overallRainfall",    1f);
                Scribe_Values.Look(ref overallTemperature, "overallTemperature", 1f);
                Scribe_Values.Look(ref overallPopulation,  "overallPopulation",  1f);
                Scribe_Values.Look(ref pollution,          "pollution",          0f);
                Scribe_Values.Look(ref coverage,           "coverage",           0.3f);
                Scribe_Collections.Look(ref settlements,   "settlements",        LookMode.Deep);
                if (settlements == null) settlements = new List<SettlementSaveData>();
            }
        }

        public class SettlementSaveData : IExposable
        {
            public string name;
            public float tileLatitude;
            public float tileLongitude;

            public void ExposeData()
            {
                Scribe_Values.Look(ref name,          "name");
                Scribe_Values.Look(ref tileLatitude,  "tileLatitude",  0f);
                Scribe_Values.Look(ref tileLongitude, "tileLongitude", 0f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Захват состояния при взлёте
        // ─────────────────────────────────────────────────────────────────────

        public static PlanetState CapturePlanetState(string nodeId, PlanetDefinition_IO planetDef)
        {
            PlanetState state = new PlanetState
            {
                nodeId             = nodeId,
                seed               = Find.World?.info?.seedString ?? "rimworld",
                overallRainfall    = planetDef?.overallRainfall    ?? 1f,
                overallTemperature = planetDef?.overallTemperature ?? 1f,
                overallPopulation  = planetDef?.overallPopulation  ?? 1f,
                pollution          = planetDef?.pollution          ?? 0f,
                coverage           = planetDef?.coverage           ?? 0.3f,
            };

            foreach (Settlement s in Find.WorldObjects.Settlements
                .Where(s => s?.Faction != null && s.Faction.IsPlayer))
            {
                Vector3 pos = Find.WorldGrid.GetTileCenter(s.Tile);
                state.settlements.Add(new SettlementSaveData
                {
                    name          = s.Name,
                    tileLatitude  = pos.y,
                    tileLongitude = Mathf.Atan2(pos.z, pos.x) * Mathf.Rad2Deg
                });
            }
            return state;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Смена планеты — GenerateWorld + восстановление faction игрока
        // ─────────────────────────────────────────────────────────────────────

        public static bool SwitchToPlanet(
            PlanetDefinition_IO destPlanetDef,
            string destNodeId,
            PlanetState existingState,
            out int landingTile)
        {
            landingTile = -1;

            if (destPlanetDef == null)
            {
                Log.Warning("[IO:PlanetSwitcher] destPlanetDef == null.");
                return false;
            }

            try
            {
                string seed        = ResolvePlanetSeed(destPlanetDef, existingState);
                float  coverage    = Find.World.info.planetCoverage;
                float  rainfall    = destPlanetDef.useVanillaDefaults ? (float)Find.World.info.overallRainfall    : destPlanetDef.overallRainfall;
                float  temperature = destPlanetDef.useVanillaDefaults ? (float)Find.World.info.overallTemperature : destPlanetDef.overallTemperature;
                float  population  = destPlanetDef.useVanillaDefaults ? (float)Find.World.info.overallPopulation  : destPlanetDef.overallPopulation;

                // ── 1. Сохраняем фракцию игрока ДО GenerateWorld ─────────────
                Faction oldPlayerFaction = Find.FactionManager?.OfPlayer;
                if (oldPlayerFaction == null)
                {
                    Log.Warning("[IO:PlanetSwitcher] Нет фракции игрока перед GenerateWorld.");
                }

                // ── 2. Перегенерируем мир ─────────────────────────────────────
                WorldGenerator.GenerateWorld(
                    coverage,
                    seed,
                    FloatToOverallRainfall(rainfall),
                    FloatToOverallTemperature(temperature),
                    FloatToOverallPopulation(population),
                    LandmarkDensity.Normal);

                try { Find.World.FinalizeInit(true); }
                catch (Exception ex) { Log.Warning("[IO:PlanetSwitcher] FinalizeInit: " + ex.Message); }

                // ── 3. Восстанавливаем faction игрока ────────────────────────
                // GenerateWorld создал новую Faction в FactionManager.
                // Заменяем новую faction нашей старой через reflection —
                // тогда все существующие ссылки в пешках/строениях остаются валидными.
                if (oldPlayerFaction != null)
                    RestorePlayerFaction(oldPlayerFaction);

                landingTile = FindLandingTile();

                Log.Message("[IO:PlanetSwitcher] Планета сменена. seed=" + seed + " tile=" + landingTile);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[IO:PlanetSwitcher] SwitchToPlanet failed: " + ex);
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Восстановление faction игрока через reflection
        // ─────────────────────────────────────────────────────────────────────

        private static void RestorePlayerFaction(Faction oldPlayerFaction)
{
    try
    {
        FactionManager fm = Find.FactionManager;
        if (fm == null || oldPlayerFaction == null) return;

        Log.Message("[IO:PlanetSwitcher] FactionManager fields: " +
            string.Join(", ", typeof(FactionManager)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(f => f.Name + "(" + f.FieldType.Name + ")")));

        Faction newPlayerFaction = null;
        try { newPlayerFaction = fm.OfPlayer; } catch { }
        if (newPlayerFaction == null)
            newPlayerFaction = fm.AllFactions.FirstOrDefault(f => f != null && f.IsPlayer);

        Log.Message("[IO:PlanetSwitcher] После GenerateWorld: allFactions.Count=" +
            fm.AllFactions.Count() +
            " newPlayerFaction=" + (newPlayerFaction?.Name ?? "null") +
            " oldPlayerFaction=" + oldPlayerFaction.Name +
            " oldIsPlayer=" + oldPlayerFaction.IsPlayer);

        if (newPlayerFaction != null && newPlayerFaction != oldPlayerFaction)
        {
            foreach (Faction npcFaction in fm.AllFactions.ToList())
            {
                if (npcFaction == null || npcFaction == newPlayerFaction) continue;
                ReplaceRelationTarget(npcFaction, newPlayerFaction, oldPlayerFaction);
            }

            ReplaceFactionInManager(fm, newPlayerFaction, oldPlayerFaction);
        }
        else
        {
            AddFactionToManager(fm, oldPlayerFaction);
        }

        ForceSetPlayerFaction(fm, oldPlayerFaction);
        EnsurePlayerRelations(fm, oldPlayerFaction);

        Faction check = null;
        try { check = fm.OfPlayer; } catch { }
        if (check == null)
            check = fm.AllFactions.FirstOrDefault(f => f != null && f.IsPlayer);

        Log.Message("[IO:PlanetSwitcher] После восстановления: OfPlayer=" + (check?.Name ?? "null"));
    }
    catch (Exception ex)
    {
        Log.Error("[IO:PlanetSwitcher] RestorePlayerFaction failed: " + ex);
    }
}



private static void ForceSetPlayerFaction(FactionManager fm, Faction playerFaction)
{
    if (fm == null || playerFaction == null) return;

    try
    {
        FieldInfo allFactionsField = typeof(FactionManager).GetField(
            "allFactions", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        List<Faction> allFactions = allFactionsField?.GetValue(fm) as List<Faction>;
        if (allFactions != null && !allFactions.Contains(playerFaction))
            allFactions.Add(playerFaction);

        foreach (FieldInfo field in typeof(FactionManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (field.FieldType != typeof(Faction))
                continue;

            string lower = field.Name.ToLowerInvariant();
            if (lower.Contains("player") || lower.Contains("ofplayer"))
            {
                try { field.SetValue(fm, playerFaction); }
                catch { }
            }
        }
    }
    catch (Exception ex)
    {
        Log.Warning("[IO:PlanetSwitcher] ForceSetPlayerFaction failed: " + ex);
    }
}

private static void EnsurePlayerRelations(FactionManager fm, Faction playerFaction)
{
    if (fm == null || playerFaction == null) return;

    foreach (Faction other in fm.AllFactionsListForReading)
    {
        if (other == null || other == playerFaction)
            continue;

        try
        {
            playerFaction.RelationWith(other, true);
        }
        catch
        {
        }

        try
        {
            other.RelationWith(playerFaction, true);
        }
        catch
        {
        }

        ReplaceRelationTarget(other, null, playerFaction);
    }
}

        private static void ReplaceRelationTarget(Faction faction, Faction oldTarget, Faction newTarget)
        {
            try
            {
                // FactionRelation хранит ссылку на другую faction через поле "other"
                FieldInfo relationsField = typeof(Faction).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => f.FieldType == typeof(List<FactionRelation>));
                if (relationsField == null) return;

                List<FactionRelation> relations = (List<FactionRelation>)relationsField.GetValue(faction);
                if (relations == null) return;

                FieldInfo otherField = typeof(FactionRelation).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => f.FieldType == typeof(Faction));
                if (otherField == null) return;

                foreach (FactionRelation rel in relations)
                {
                    if (rel != null && (Faction)otherField.GetValue(rel) == oldTarget)
                        otherField.SetValue(rel, newTarget);
                }
            }
            catch { }
        }

        private static void ReplaceFactionInManager(FactionManager fm, Faction toRemove, Faction toAdd)
        {
            // Через reflection заменяем в приватном списке allFactions
            FieldInfo field = typeof(FactionManager).GetField("allFactions",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (field == null)
            {
                // Пробуем другие варианты имён
                field = typeof(FactionManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => f.FieldType == typeof(List<Faction>));
            }

            if (field == null)
            {
                Log.Warning("[IO:PlanetSwitcher] Не найдено поле List<Faction> в FactionManager.");
                return;
            }

            List<Faction> list = (List<Faction>)field.GetValue(fm);
            if (list == null) return;

            int idx = list.IndexOf(toRemove);
            if (idx >= 0)
                list[idx] = toAdd;
            else if (!list.Contains(toAdd))
                list.Add(toAdd);
        }

        private static void AddFactionToManager(FactionManager fm, Faction faction)
        {
            FieldInfo field = typeof(FactionManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType == typeof(List<Faction>));
            if (field == null) return;

            List<Faction> list = (List<Faction>)field.GetValue(fm);
            if (list != null && !list.Contains(faction))
                list.Add(faction);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Вспомогательные
        // ─────────────────────────────────────────────────────────────────────

        private static int FindLandingTile()
        {
            int count = Find.WorldGrid.TilesCount;
            if (count <= 0) return 0;
            int center = count / 2;
            for (int i = 0; i < count; i++)
            {
                int candidate = (center + i * 31) % count;
                if (!Find.WorldObjects.AnyWorldObjectAt(candidate))
                    return candidate;
            }
            return center;
        }

        private static string ResolvePlanetSeed(PlanetDefinition_IO def, PlanetState existingState)
        {
            if (existingState != null && !string.IsNullOrEmpty(existingState.seed))
                return existingState.seed;
            string baseSeed = Find.World?.info?.seedString ?? "rimworld";
            return Mathf.Abs((baseSeed + "_" + (def.id ?? "planet") + "_" + def.seedOffset).GetHashCode()).ToString();
        }

        private static OverallRainfall FloatToOverallRainfall(float v)
        {
            if (v < 0.6f) return OverallRainfall.AlmostNone;
            if (v > 1.4f) return OverallRainfall.VeryHigh;
            if (v > 1.1f) return OverallRainfall.High;
            return OverallRainfall.Normal;
        }

        private static OverallTemperature FloatToOverallTemperature(float v)
        {
            if (v < 0.5f) return OverallTemperature.VeryCold;
            if (v < 0.85f) return OverallTemperature.Cold;
            if (v > 1.6f) return OverallTemperature.VeryHot;
            if (v > 1.3f) return OverallTemperature.Hot;
            return OverallTemperature.Normal;
        }

        private static OverallPopulation FloatToOverallPopulation(float v)
        {
            if (v < 0.4f) return OverallPopulation.AlmostNone;
            if (v > 1.7f) return OverallPopulation.VeryHigh;
            if (v > 1.3f) return OverallPopulation.High;
            return OverallPopulation.Normal;
        }
    }
}
