using RimWorld;
using RimWorld.Planet;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Биом вакуума. Никогда не выбирается естественно при генерации мира.
    /// </summary>
    public class BiomeWorker_VoidSpace : BiomeWorker
    {
        public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
        {
            return float.MinValue;
        }
    }
}
