using System;
using Verse;

namespace InterstellarOdyssey
{
    public static class PlanetArchiveUtility
    {
        public static void PrepareArchivesForAllPlanets(GalaxyWorldConfiguration config)
        {
            if (config?.galaxies == null)
                return;

            foreach (GalaxyDefinition galaxy in config.galaxies)
            {
                if (galaxy?.planets == null)
                    continue;

                foreach (PlanetDefinition_IO planet in galaxy.planets)
                {
                    if (planet == null)
                        continue;

                    if (planet.archive == null)
                        planet.archive = new PlanetArchiveData();

                    planet.archive.seed = GeneratePlanetSeed(galaxy.id, planet.id, planet.seedOffset);
                    planet.archive.generated = true;
                    planet.archive.generatedLabel = planet.label;
                    planet.archive.cachedCoverage = planet.coverage;
                    planet.archive.cachedRainfall = planet.overallRainfall;
                    planet.archive.cachedTemperature = planet.overallTemperature;
                    planet.archive.cachedPopulation = planet.overallPopulation;
                    planet.archive.cachedPollution = planet.pollution;
                    planet.archive.cachedWorldTileCount = Find.WorldGrid != null ? Find.WorldGrid.TilesCount : 0;
                }
            }
        }

        private static string GeneratePlanetSeed(string galaxyId, string planetId, int seedOffset)
        {
            string baseSeed = Find.World?.info?.seedString ?? "interstellar_odyssey";
            return baseSeed + "_" + galaxyId + "_" + planetId + "_" + seedOffset;
        }
    }
}
