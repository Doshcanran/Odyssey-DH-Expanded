using Verse;

namespace InterstellarOdyssey
{
    public static class ShipFloorUtility
    {
        public static bool IsShipFloor(TerrainDef terrain)
        {
            return ShipPartUtility.IsShipFloor(terrain);
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
