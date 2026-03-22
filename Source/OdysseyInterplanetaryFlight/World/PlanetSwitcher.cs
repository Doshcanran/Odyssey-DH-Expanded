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
        //  Смена планеты — GenerateWorld через reflection + восстановление Maps
        //
        //  Вызываем WorldGenerator.GenerateWorld чтобы планета реально менялась.
        //  ДО вызова сохраняем Maps и FactionManager, ПОСЛЕ — восстанавливаем.
        //  Это даёт настоящий новый мировой шар без уничтожения карт и фракций.
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
                float  rainfall    = destPlanetDef.useVanillaDefaults
                    ? (float)Find.World.info.overallRainfall    : destPlanetDef.overallRainfall;
                float  temperature = destPlanetDef.useVanillaDefaults
                    ? (float)Find.World.info.overallTemperature : destPlanetDef.overallTemperature;
                float  population  = destPlanetDef.useVanillaDefaults
                    ? (float)Find.World.info.overallPopulation  : destPlanetDef.overallPopulation;

                // ── 1. Сохраняем всё что GenerateWorld уничтожит ─────────────
                List<Map>      savedMaps            = Current.Game.Maps.ToList();
                FactionManager savedFM              = Find.FactionManager;
                List<Faction>  savedFactions        = GetAllFactionsList(savedFM)?.ToList();
                Map            savedCurrentMap      = Current.Game.CurrentMap;
                // Сохраняем наш WorldComponent — GenerateWorld его пересоздаёт
                WorldComponent_Interstellar savedWorldComponent =
                    Find.World?.GetComponent<WorldComponent_Interstellar>();

                Log.Message("[IO:PlanetSwitcher] Сохранено карт: " + savedMaps.Count
                    + " фракций: " + (savedFactions?.Count ?? 0));

                // ── 2. Вызываем GenerateWorld через reflection ─────────────────
                bool generated = TryCallGenerateWorld(coverage, seed,
                    FloatToOverallRainfall(rainfall),
                    FloatToOverallTemperature(temperature),
                    FloatToOverallPopulation(population));

                if (!generated)
                {
                    // Fallback: только обновляем World.info и перерисовываем
                    Log.Warning("[IO:PlanetSwitcher] GenerateWorld не вызван — обновляем только World.info.");
                    Find.World.info.seedString         = seed;
                    Find.World.info.overallRainfall    = FloatToOverallRainfall(rainfall);
                    Find.World.info.overallTemperature = FloatToOverallTemperature(temperature);
                    Find.World.info.overallPopulation  = FloatToOverallPopulation(population);
                    try { Find.World.renderer.RegenerateAllLayersNow(); } catch { }
                }

                // ── 3. Восстанавливаем Maps ───────────────────────────────────
                RestoreMaps(savedMaps, savedCurrentMap);

                // ── 4. Восстанавливаем FactionManager ────────────────────────
                RestoreFactionManager(savedFM, savedFactions);

                // ── 5. Восстанавливаем WorldComponent_Interstellar ───────────
                // GenerateWorld/RestoreMaps может пересоздать WorldComponents,
                // поэтому явно возвращаем наш компонент обратно в World.
                RestoreWorldComponent(savedWorldComponent);

                landingTile = FindLandingTile();

                Log.Message("[IO:PlanetSwitcher] Планета сменена. seed=" + seed
                    + " tile=" + landingTile
                    + " OfPlayer=" + (SafeOfPlayer()?.Name ?? "null")
                    + " Maps=" + Current.Game.Maps.Count);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[IO:PlanetSwitcher] SwitchToPlanet failed: " + ex);
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Вызов WorldGenerator.GenerateWorld через reflection
        //  (сигнатура изменилась в 1.6, используем reflection для совместимости)
        // ─────────────────────────────────────────────────────────────────────

        private static bool TryCallGenerateWorld(
            float coverage, string seed,
            OverallRainfall rainfall, OverallTemperature temperature, OverallPopulation population)
        {
            try
            {
                MethodInfo method = typeof(WorldGenerator).GetMethod("GenerateWorld",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                if (method == null)
                {
                    Log.Warning("[IO:PlanetSwitcher] WorldGenerator.GenerateWorld не найден.");
                    return false;
                }

                ParameterInfo[] parms = method.GetParameters();
                object[] args = new object[parms.Length];

                for (int i = 0; i < parms.Length; i++)
                {
                    Type   pt = parms[i].ParameterType;
                    string pn = parms[i].Name?.ToLowerInvariant() ?? "";

                    if (pt == typeof(float) && (pn.Contains("coverage") || pn.Contains("planet")))
                        args[i] = coverage;
                    else if (pt == typeof(string) && pn.Contains("seed"))
                        args[i] = seed;
                    else if (pt == typeof(OverallRainfall))
                        args[i] = rainfall;
                    else if (pt == typeof(OverallTemperature))
                        args[i] = temperature;
                    else if (pt == typeof(OverallPopulation))
                        args[i] = population;
                    else if (pt.IsEnum)
                        args[i] = Enum.ToObject(pt, 0); // LandmarkDensity.Normal = 0
                    else if (pt == typeof(bool))
                        args[i] = false;
                    else if (pt.IsValueType)
                        args[i] = Activator.CreateInstance(pt);
                    else
                        args[i] = null;
                }

                method.Invoke(null, args);
                Log.Message("[IO:PlanetSwitcher] GenerateWorld вызван. Параметров: " + parms.Length);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[IO:PlanetSwitcher] TryCallGenerateWorld: " + ex.Message);
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Восстановление Maps после GenerateWorld
        // ─────────────────────────────────────────────────────────────────────

        private static void RestoreMaps(List<Map> savedMaps, Map savedCurrentMap)
        {
            if (savedMaps == null || savedMaps.Count == 0) return;
            try
            {
                FieldInfo mapsField = typeof(Game)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => f.FieldType == typeof(List<Map>));

                if (mapsField == null)
                {
                    Log.Warning("[IO:PlanetSwitcher] RestoreMaps: поле List<Map> не найдено в Game.");
                    return;
                }

                List<Map> currentMaps = mapsField.GetValue(Current.Game) as List<Map>;
                if (currentMaps == null)
                    mapsField.SetValue(Current.Game, savedMaps);
                else
                {
                    foreach (Map m in savedMaps)
                        if (!currentMaps.Contains(m))
                            currentMaps.Add(m);
                }

                try { if (savedCurrentMap != null) Current.Game.CurrentMap = savedCurrentMap; } catch { }

                Log.Message("[IO:PlanetSwitcher] RestoreMaps: " + savedMaps.Count + " карт.");
            }
            catch (Exception ex)
            {
                Log.Warning("[IO:PlanetSwitcher] RestoreMaps: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Восстановление FactionManager после GenerateWorld
        // ─────────────────────────────────────────────────────────────────────

        private static void RestoreFactionManager(FactionManager savedFM, List<Faction> savedFactions)
        {
            if (savedFM == null) return;
            try
            {
                // Возвращаем сохранённый FactionManager в World
                FieldInfo fmField = typeof(World)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => f.FieldType == typeof(FactionManager));
                if (fmField != null)
                    fmField.SetValue(Find.World, savedFM);

                // Восстанавливаем список фракций
                if (savedFactions != null)
                {
                    List<Faction> fmList = GetAllFactionsList(savedFM);
                    if (fmList != null)
                    {
                        fmList.Clear();
                        fmList.AddRange(savedFactions);
                    }
                }

                Log.Message("[IO:PlanetSwitcher] FactionManager восстановлен. OfPlayer="
                    + (SafeOfPlayer()?.Name ?? "null"));
            }
            catch (Exception ex)
            {
                Log.Warning("[IO:PlanetSwitcher] RestoreFactionManager: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Восстановление WorldComponent_Interstellar после GenerateWorld
        // ─────────────────────────────────────────────────────────────────────

        private static void RestoreWorldComponent(WorldComponent_Interstellar saved)
        {
            if (saved == null || Find.World == null) return;
            try
            {
                // Получаем список WorldComponents через reflection
                FieldInfo field = typeof(World).GetFields(
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => f.FieldType == typeof(List<WorldComponent>)
                                     || f.Name.ToLowerInvariant().Contains("component"));

                if (field == null)
                {
                    Log.Warning("[IO:PlanetSwitcher] RestoreWorldComponent: поле WorldComponents не найдено.");
                    return;
                }

                List<WorldComponent> comps = field.GetValue(Find.World) as List<WorldComponent>;
                if (comps == null) return;

                // Заменяем новосозданный компонент нашим сохранённым
                for (int i = 0; i < comps.Count; i++)
                {
                    if (comps[i] is WorldComponent_Interstellar)
                    {
                        comps[i] = saved;
                        Log.Message("[IO:PlanetSwitcher] WorldComponent_Interstellar восстановлен."
                            + " currentPlanetNodeId=" + saved.currentPlanetNodeId
                            + " shipLocations=" + saved.shipLocations?.Count);
                        return;
                    }
                }

                // Если не нашли — добавляем
                comps.Add(saved);
                Log.Message("[IO:PlanetSwitcher] WorldComponent_Interstellar добавлен (не был в списке).");
            }
            catch (Exception ex)
            {
                Log.Warning("[IO:PlanetSwitcher] RestoreWorldComponent: " + ex.Message);
            }
        }

        private static List<Faction> GetAllFactionsList(FactionManager fm)
        {
            if (fm == null) return null;
            FieldInfo field = typeof(FactionManager).GetField(
                "allFactions", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                field = typeof(FactionManager)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => f.FieldType == typeof(List<Faction>));
            return field?.GetValue(fm) as List<Faction>;
        }

        private static Faction SafeOfPlayer()
        {
            try { return Find.FactionManager?.OfPlayer; }
            catch { return Find.FactionManager?.AllFactionsListForReading
                ?.FirstOrDefault(f => f?.IsPlayer == true); }
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
