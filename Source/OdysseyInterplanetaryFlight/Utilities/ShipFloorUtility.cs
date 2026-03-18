using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public static class ShipFloorUtility
        {
            private static readonly HashSet<string> ShipTerrainDefNames = new HashSet<string>
            {
                "GravshipDeck",
                "GravshipFloor",
                "GravshipSuperstructure",
                "ShipDeck",
                "ShipFloor",
                "Substructure",
                "Substruscure"
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
                    || defName.Contains("substruscure")
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
                return IsShipFloor(terrain);
            }
        }
}
