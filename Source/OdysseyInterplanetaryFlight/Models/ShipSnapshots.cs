using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class ShipThingSnapshot : IExposable
        {
            public Thing thing;
            public IntVec3 offset = IntVec3.Zero;
            public Rot4 rotation = Rot4.North;

            // Явно сохраняем числовое значение ротации (0=N,1=E,2=S,3=W),
            // чтобы не зависеть от внутреннего состояния Thing после деспавна и загрузки сейва.
            private int rotationInt = 0;

            public void ExposeData()
            {
                Scribe_Deep.Look(ref thing, "thing");
                Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);

                // При сохранении — берём из rotation, при загрузке — восстанавливаем в rotation
                if (Scribe.mode == LoadSaveMode.Saving)
                    rotationInt = rotation.AsInt;

                Scribe_Values.Look(ref rotationInt, "rotationInt", 0);

                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                    rotation = new Rot4(rotationInt);

                // Для обратной совместимости со старыми сейвами, где rotationInt не было
                Scribe_Values.Look(ref rotation, "rotation", Rot4.North);

                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                    rotation = new Rot4(rotationInt);
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
            /// <summary>
            /// Переназначает фракцию всех объектов снапшота на текущую фракцию игрока.
            /// Вызывается после WorldGenerator.GenerateWorld, так как GenerateWorld
            /// создаёт НОВЫЙ объект Faction для игрока — старые ссылки становятся невалидными.
            /// </summary>
            public void ReassignToPlayerFaction()
            {
                Faction newPlayer = null;
                try { newPlayer = Find.FactionManager?.OfPlayer; } catch { }
                if (newPlayer == null)
                {
                    try
                    {
                        newPlayer = Find.FactionManager?.AllFactions
                            ?.FirstOrDefault(f => f != null && f.IsPlayer);
                    }
                    catch { }
                }
                if (newPlayer == null)
                {
                    Log.Warning("[IO:Snapshot] ReassignToPlayerFaction: не удалось найти фракцию игрока.");
                    return;
                }

                // Здания — все объекты в снапшоте принадлежали игроку, назначаем напрямую
                if (buildings != null)
                    foreach (ShipThingSnapshot s in buildings)
                    {
                        if (s?.thing == null) continue;
                        // SetFactionDirect — без побочных эффектов, работает на деспавненных
                        s.thing.SetFactionDirect(newPlayer);
                    }

                // Предметы
                if (items != null)
                    foreach (ShipThingSnapshot s in items)
                    {
                        if (s?.thing == null) continue;
                        // Предметы могут не иметь faction — устанавливаем только если поддерживают
                        try { s.thing.SetFactionDirect(newPlayer); } catch { }
                    }

                // Пешки — SetFaction с полным обновлением внутренних связей
                if (pawns != null)
                    foreach (ShipPawnSnapshot s in pawns)
                    {
                        if (s?.pawn == null || s.pawn.Dead) continue;
                        try { s.pawn.SetFaction(newPlayer); }
                        catch (Exception ex)
                        {
                            Log.Warning("[IO:Snapshot] SetFaction пешки "
                                + s.pawn.LabelShortCap + ": " + ex.Message);
                        }
                    }

                Log.Message("[IO:Snapshot] ReassignToPlayerFaction: переназначено "
                    + (buildings?.Count ?? 0) + " зданий, "
                    + (items?.Count ?? 0) + " предметов, "
                    + (pawns?.Count ?? 0) + " пешек.");
            }

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
}
