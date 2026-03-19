using RimWorld;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Заполняет всю карту полом вакуума IO_VoidFloor и снимает туман войны.
    /// Кроме того убирает все крыши — в открытом космосе их нет.
    /// </summary>
    public class GenStep_IO_VoidTerrain : GenStep
    {
        public override int SeedPart => 0x494F5654; // "IOVT"

        public override void Generate(Map map, GenStepParams parms)
        {
            TerrainDef voidFloor = DefDatabase<TerrainDef>.GetNamedSilentFail("IO_VoidFloor")
                                   ?? TerrainDefOf.PackedDirt;

            foreach (IntVec3 cell in map.AllCells)
            {
                // Ставим тёмный пол вакуума
                map.terrainGrid.SetTerrain(cell, voidFloor);

                // Убираем туман войны — игрок должен сразу видеть корабль
                map.fogGrid.Unfog(cell);

                // Убираем все крыши — в открытом космосе их нет
                if (map.roofGrid.RoofAt(cell) != null)
                    map.roofGrid.SetRoof(cell, null);
            }
        }
    }
}
