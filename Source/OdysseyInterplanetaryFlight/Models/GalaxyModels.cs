using System;
using System.Collections.Generic;
using Verse;

namespace InterstellarOdyssey
{
    public class GalaxyWorldConfiguration : IExposable
    {
        public int selectedGalaxyCount = 1;
        public List<GalaxyDefinition> galaxies = new List<GalaxyDefinition>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref selectedGalaxyCount, "selectedGalaxyCount", 1);
            Scribe_Collections.Look(ref galaxies, "galaxies", LookMode.Deep);

            if (galaxies == null)
                galaxies = new List<GalaxyDefinition>();
        }
    }

    public class GalaxyDefinition : IExposable
    {
        public string id;
        public string label;
        public bool hasStations = true;
        public bool hasAsteroidBelts = true;
        public bool hasPlanets = true;
        public int stationCount = 1;
        public int beltCount = 1;
        public int planetCount = 3;
        public string solarSystemId;
        public List<PlanetDefinition_IO> planets = new List<PlanetDefinition_IO>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref hasStations, "hasStations", true);
            Scribe_Values.Look(ref hasAsteroidBelts, "hasAsteroidBelts", true);
            Scribe_Values.Look(ref hasPlanets, "hasPlanets", true);
            Scribe_Values.Look(ref stationCount, "stationCount", 1);
            Scribe_Values.Look(ref beltCount, "beltCount", 1);
            Scribe_Values.Look(ref planetCount, "planetCount", 3);
            Scribe_Values.Look(ref solarSystemId, "solarSystemId");
            Scribe_Collections.Look(ref planets, "planets", LookMode.Deep);

            if (planets == null)
                planets = new List<PlanetDefinition_IO>();
        }
    }

    public class PlanetDefinition_IO : IExposable
    {
        public string id;
        public string label;
        public float overallRainfall = 1f;
        public float overallTemperature = 1f;
        public float overallPopulation = 1f;
        public float pollution = 0f;
        public float coverage = 0.3f;
        public int seedOffset;
        public bool startPlanet;
        public bool useVanillaDefaults = true;
        public PlanetArchiveData archive;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref overallRainfall, "overallRainfall", 1f);
            Scribe_Values.Look(ref overallTemperature, "overallTemperature", 1f);
            Scribe_Values.Look(ref overallPopulation, "overallPopulation", 1f);
            Scribe_Values.Look(ref pollution, "pollution", 0f);
            Scribe_Values.Look(ref coverage, "coverage", 0.3f);
            Scribe_Values.Look(ref seedOffset, "seedOffset", 0);
            Scribe_Values.Look(ref startPlanet, "startPlanet", false);
            Scribe_Values.Look(ref useVanillaDefaults, "useVanillaDefaults", true);
            Scribe_Deep.Look(ref archive, "archive");
        }
    }

    public class PlanetArchiveData : IExposable
    {
        public string seed;
        public int cachedWorldTileCount;
        public bool generated;
        public bool visited;
        public string generatedLabel;
        public float cachedCoverage;
        public float cachedRainfall;
        public float cachedTemperature;
        public float cachedPopulation;
        public float cachedPollution;

        public void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref cachedWorldTileCount, "cachedWorldTileCount", 0);
            Scribe_Values.Look(ref generated, "generated", false);
            Scribe_Values.Look(ref visited, "visited", false);
            Scribe_Values.Look(ref generatedLabel, "generatedLabel");
            Scribe_Values.Look(ref cachedCoverage, "cachedCoverage", 0.3f);
            Scribe_Values.Look(ref cachedRainfall, "cachedRainfall", 1f);
            Scribe_Values.Look(ref cachedTemperature, "cachedTemperature", 1f);
            Scribe_Values.Look(ref cachedPopulation, "cachedPopulation", 1f);
            Scribe_Values.Look(ref cachedPollution, "cachedPollution", 0f);
        }
    }
}
