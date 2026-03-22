using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Архивирует (сохраняет на диск) и восстанавливает карты (Map) при смене планеты.
    ///
    /// Save:  SafeSaver.Save → Scribe_Deep.Look(ref map)
    /// Load:  Scribe.loader.InitLoading → Scribe_Deep.Look(ref map) → внедряем в Game
    ///
    /// Файлы: .../Saves/IO_MapArchive/<мир>/<nodeId>_<tile>_<имя>.xml
    /// </summary>
    public static class MapArchiver
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Сохранение всех карт планеты
        // ─────────────────────────────────────────────────────────────────────

        public static List<ArchivedMapMeta> ArchiveAllMaps(string planetNodeId)
        {
            List<ArchivedMapMeta> result = new List<ArchivedMapMeta>();
            if (string.IsNullOrEmpty(planetNodeId)) return result;

            string dir = GetArchiveDir();
            Directory.CreateDirectory(dir);

            foreach (Map map in Find.Maps.ToList())
            {
                if (map == null) continue;
                ArchivedMapMeta meta = SaveMapToFile(map, planetNodeId, dir);
                if (meta != null)
                    result.Add(meta);
            }

            Log.Message("[IO:MapArchiver] Заархивировано карт: " + result.Count
                + " для планеты «" + planetNodeId + "»");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Загрузка карт планеты
        // ─────────────────────────────────────────────────────────────────────

        public static Map RestoreArchivedMaps(string planetNodeId, List<ArchivedMapMeta> metas)
        {
            if (string.IsNullOrEmpty(planetNodeId) || metas == null || metas.Count == 0)
                return null;

            string dir = GetArchiveDir();
            Map firstRestored = null;

            foreach (ArchivedMapMeta meta in metas)
            {
                if (meta == null || string.IsNullOrEmpty(meta.filePath)) continue;
                string fullPath = Path.Combine(dir, meta.filePath);
                if (!File.Exists(fullPath))
                {
                    Log.Warning("[IO:MapArchiver] Файл не найден: " + fullPath);
                    continue;
                }

                Map restored = LoadMapFromFile(fullPath, meta);
                if (restored != null && firstRestored == null)
                    firstRestored = restored;
            }

            Log.Message("[IO:MapArchiver] Восстановлено карт для «" + planetNodeId + "»");
            return firstRestored;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Запись одной карты в файл
        // ─────────────────────────────────────────────────────────────────────

        private static ArchivedMapMeta SaveMapToFile(Map map, string planetNodeId, string dir)
        {
            if (map?.Parent == null) return null;

            int    tile     = map.Parent.Tile;
            string safeName = SanitizeFileName(map.Parent.LabelCap ?? "map");
            string fileName = planetNodeId + "_" + tile + "_" + safeName + ".xml";
            string fullPath = Path.Combine(dir, fileName);

            try
            {
                Map capturedMap = map;

                SafeSaver.Save(fullPath, "IOMapArchive", delegate
                {
                    int    t           = tile;
                    string name        = capturedMap.Parent?.LabelCap ?? "map";
                    bool   isHome      = capturedMap.IsPlayerHome;
                    string factionId   = capturedMap.ParentFaction?.GetUniqueLoadID() ?? "";

                    Scribe_Values.Look(ref t,         "tile",         0);
                    Scribe_Values.Look(ref name,      "mapName",      "");
                    Scribe_Values.Look(ref isHome,    "isPlayerHome", false);
                    Scribe_Values.Look(ref factionId, "factionId",    "");
                    Scribe_Deep.Look(ref capturedMap, "map");
                }, false);

                return new ArchivedMapMeta
                {
                    tile         = tile,
                    mapName      = map.Parent.LabelCap ?? "map",
                    isPlayerHome = map.IsPlayerHome,
                    planetNodeId = planetNodeId,
                    filePath     = fileName,
                    factionId    = map.ParentFaction?.GetUniqueLoadID() ?? ""
                };
            }
            catch (Exception ex)
            {
                Log.Error("[IO:MapArchiver] Ошибка сохранения карты tile=" + tile + ": " + ex);
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Загрузка одной карты из файла
        // ─────────────────────────────────────────────────────────────────────

        private static Map LoadMapFromFile(string fullPath, ArchivedMapMeta meta)
        {
            try
            {
                int tile = FindBestTile(meta.tile);
                if (tile < 0)
                {
                    Log.Warning("[IO:MapArchiver] Нет тайла для карты «" + meta.mapName + "»");
                    return null;
                }

                MapParent parent = EnsureMapParent(tile, meta);
                if (parent == null) return null;

                Map map = null;

                // В RimWorld 1.6 ScribeLoader — instance через Scribe.loader
                Scribe.loader.InitLoading(fullPath);
                try
                {
                    if (Scribe.EnterNode("IOMapArchive"))
                    {
                        Scribe_Deep.Look(ref map, "map", parent);
                        Scribe.ExitNode();
                    }
                }
                finally
                {
                    Scribe.loader.FinalizeLoading();
                }

                if (map == null)
                {
                    Log.Warning("[IO:MapArchiver] map == null после загрузки из " + fullPath);
                    Find.WorldObjects.Remove(parent);
                    return null;
                }

                AddMapToGame(map, parent, meta.isPlayerHome);
                Log.Message("[IO:MapArchiver] Восстановлена карта tile=" + tile + " «" + meta.mapName + "»");
                return map;
            }
            catch (Exception ex)
            {
                Log.Error("[IO:MapArchiver] Ошибка загрузки из " + fullPath + ": " + ex);
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Внедрение карты в Game через reflection
        // ─────────────────────────────────────────────────────────────────────

        private static void AddMapToGame(Map map, MapParent parent, bool isPlayerHome)
        {
            // Game.maps — приватный List<Map>
            var mapsField = typeof(Game).GetField("maps",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            List<Map> gameMaps = mapsField?.GetValue(Current.Game) as List<Map>;
            if (gameMaps == null)
            {
                var addMap = typeof(Game).GetMethod("AddMap",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance);
                if (addMap != null)
                    addMap.Invoke(Current.Game, new object[] { map });
                else
                    Log.Error("[IO:MapArchiver] Не удалось найти Game.maps или Game.AddMap");
                return;
            }

            if (!gameMaps.Contains(map))
                gameMaps.Add(map);

            // Устанавливаем фракцию на MapParent ПЕРЕД FinalizeInit
            // чтобы Map.IsPlayerHome не бросал исключение
            if (parent != null)
            {
                Faction playerFaction = SafeGetPlayerFaction();
                if (playerFaction != null && parent.Faction != playerFaction)
                    parent.SetFaction(playerFaction);
            }

            try { map.FinalizeInit(); }
            catch (Exception ex) { Log.Warning("[IO:MapArchiver] FinalizeInit: " + ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Создание WorldObject-держателя карты
        // ─────────────────────────────────────────────────────────────────────

        private static MapParent EnsureMapParent(int tile, ArchivedMapMeta meta)
        {
            // Проверяем что тайл валиден в текущем мире
            int tileCount = Find.WorldGrid?.TilesCount ?? 0;
            if (tile < 0 || tile >= tileCount)
            {
                Log.Warning("[IO:MapArchiver] EnsureMapParent: тайл " + tile + " вне диапазона " + tileCount);
                return null;
            }

            WorldObject existing = Find.WorldObjects.WorldObjectAt(tile, WorldObjectDefOf.Settlement);
            if (existing is MapParent mp) return mp;

            Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(
                WorldObjectDefOf.Settlement);
            settlement.Tile = tile;
            settlement.Name = meta.mapName ?? "Поселение";

            // Безопасно получаем фракцию игрока — может быть null сразу после GenerateWorld
            Faction playerFaction = SafeGetPlayerFaction();
            if (playerFaction != null)
                settlement.SetFaction(playerFaction);

            Find.WorldObjects.Add(settlement);
            return settlement;
        }

        /// <summary>
        /// Безопасно возвращает фракцию игрока, не бросая исключений.
        /// После WorldGenerator.GenerateWorld FactionManager может быть не инициализирован.
        /// </summary>
        private static Faction SafeGetPlayerFaction()
        {
            try
            {
                if (Find.FactionManager == null) return null;
                Faction f = Find.FactionManager.OfPlayer;
                return f;
            }
            catch
            {
                // OfPlayer бросает если фракция не найдена
                try
                {
                    return Find.FactionManager?.AllFactions
                        ?.FirstOrDefault(f2 => f2 != null && f2.IsPlayer);
                }
                catch { return null; }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Поиск свободного тайла — без обращения к Tile.biome
        // ─────────────────────────────────────────────────────────────────────

        private static int FindBestTile(int originalTile)
        {
            int count = Find.WorldGrid.TilesCount;
            if (count <= 0) return -1;

            // Сначала пробуем тот же тайл
            if (originalTile >= 0 && originalTile < count
                && !Find.WorldObjects.AnyWorldObjectAt(originalTile))
                return originalTile;

            // Ищем свободный тайл рядом — тот же подход что OrbitalNodeMapUtility
            int base_ = originalTile >= 0 && originalTile < count ? originalTile : count / 2;
            for (int i = 1; i < count; i++)
            {
                int candidate = (base_ + i * 37) % count;
                if (!Find.WorldObjects.AnyWorldObjectAt(candidate))
                    return candidate;
            }
            return -1;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Путь к архиву
        // ─────────────────────────────────────────────────────────────────────

        private static string GetArchiveDir()
        {
            string savesDir = GenFilePaths.SavedGamesFolderPath;
            string worldName = SanitizeFileName(
                Find.World?.info?.name
                ?? Current.Game?.Info?.permadeathModeUniqueName
                ?? "unknown");
            return Path.Combine(savesDir, "IO_MapArchive", worldName);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "map";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 40 ? name.Substring(0, 40) : name;
        }

        public static void DeleteArchive(string planetNodeId)
        {
            try
            {
                string dir = GetArchiveDir();
                if (!Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir, planetNodeId + "_*.xml"))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                Log.Warning("[IO:MapArchiver] DeleteArchive: " + ex.Message);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Метаданные заархивированной карты
    // ─────────────────────────────────────────────────────────────────────────

    public class ArchivedMapMeta : IExposable
    {
        public int    tile;
        public string mapName;
        public bool   isPlayerHome;
        public string planetNodeId;
        public string filePath;
        public string factionId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tile,         "tile",         0);
            Scribe_Values.Look(ref mapName,      "mapName");
            Scribe_Values.Look(ref isPlayerHome, "isPlayerHome", false);
            Scribe_Values.Look(ref planetNodeId, "planetNodeId");
            Scribe_Values.Look(ref filePath,     "filePath");
            Scribe_Values.Look(ref factionId,    "factionId");
        }
    }
}
