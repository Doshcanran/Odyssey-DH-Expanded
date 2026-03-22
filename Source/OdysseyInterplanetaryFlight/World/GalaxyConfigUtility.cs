using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace InterstellarOdyssey
{
    public static class GalaxyConfigUtility
    {
        private static readonly string[] NamePrefixes =
        {
            "Ar", "Bel", "Cer", "Dra", "Ely", "Fen", "Hel", "Ira", "Jor", "Kal",
            "Lum", "Mor", "Nex", "Or", "Pra", "Qua", "Ryn", "Sol", "Tal", "Vel",
            "Xan", "Yor", "Zer"
        };

        private static readonly string[] NameMiddles =
        {
            "a", "e", "i", "o", "u", "ae", "ia", "io", "ar", "or", "en", "un"
        };

        private static readonly string[] NameSuffixes =
        {
            "lon", "ria", "tis", "nus", "mia", "dor", "vek", "ion", "ara", "eth",
            " Prime", " Secundus", " III", " IV", " V"
        };

        public static GalaxyWorldConfiguration CreateDefaultConfiguration()
        {
            GalaxyWorldConfiguration config = new GalaxyWorldConfiguration();
            config.selectedGalaxyCount = 1;
            config.galaxies = new List<GalaxyDefinition>
            {
                CreateDefaultGalaxy(0, true)
            };
            EnsureConsistency(config);
            return config;
        }

        public static GalaxyDefinition CreateDefaultGalaxy(int index, bool withStartPlanet)
        {
            string worldName = Find.World?.info?.name ?? "RimWorld";
            Random random = CreateRandom(index);

            GalaxyDefinition galaxy = new GalaxyDefinition
            {
                id = "galaxy_" + index,
                label = index == 0 ? "Галактика игрока" : "Галактика " + (index + 1),
                solarSystemId = "system_" + index,
                hasStations = true,
                hasAsteroidBelts = true,
                hasPlanets = true,
                stationCount = 1,
                beltCount = 1,
                planetCount = 3
            };

            galaxy.planets.Add(new PlanetDefinition_IO
            {
                id = withStartPlanet ? "homeworld" : galaxy.id + "_planet_0",
                label = withStartPlanet ? worldName : GeneratePlanetName(random),
                startPlanet = withStartPlanet,
                useVanillaDefaults = true,
                seedOffset = GenerateSeedOffset(random, index, 0)
            });
            galaxy.planets.Add(CreateRandomPlanet(galaxy.id, 1, random));
            galaxy.planets.Add(CreateRandomPlanet(galaxy.id, 2, random));

            return galaxy;
        }

        public static void EnsureConsistency(GalaxyWorldConfiguration config)
        {
            if (config == null)
                return;

            if (config.galaxies == null)
                config.galaxies = new List<GalaxyDefinition>();

            config.selectedGalaxyCount = Math.Max(1, config.selectedGalaxyCount);

            while (config.galaxies.Count < config.selectedGalaxyCount)
                config.galaxies.Add(CreateDefaultGalaxy(config.galaxies.Count, config.galaxies.Count == 0));

            if (config.galaxies.Count > config.selectedGalaxyCount)
                config.galaxies.RemoveRange(config.selectedGalaxyCount, config.galaxies.Count - config.selectedGalaxyCount);

            bool startPlanetAssigned = false;
            for (int i = 0; i < config.galaxies.Count; i++)
            {
                GalaxyDefinition galaxy = config.galaxies[i] ?? CreateDefaultGalaxy(i, !startPlanetAssigned);
                config.galaxies[i] = galaxy;

                if (string.IsNullOrEmpty(galaxy.id))
                    galaxy.id = "galaxy_" + i;
                if (string.IsNullOrEmpty(galaxy.label))
                    galaxy.label = "Галактика " + (i + 1);
                if (string.IsNullOrEmpty(galaxy.solarSystemId))
                    galaxy.solarSystemId = "system_" + i;

                galaxy.stationCount = Math.Max(1, galaxy.stationCount);
                galaxy.beltCount = Math.Max(1, galaxy.beltCount);
                galaxy.planetCount = Math.Max(1, galaxy.planetCount);

                if (galaxy.planets == null)
                    galaxy.planets = new List<PlanetDefinition_IO>();

                Random random = CreateRandom(i + galaxy.planets.Count);

                while (galaxy.planets.Count < galaxy.planetCount)
                {
                    int pIndex = galaxy.planets.Count;
                    bool isStartPlanet = !startPlanetAssigned && i == 0 && pIndex == 0;
                    galaxy.planets.Add(isStartPlanet
                        ? new PlanetDefinition_IO
                        {
                            id = "homeworld",
                            label = Find.World?.info?.name ?? "RimWorld",
                            startPlanet = true,
                            useVanillaDefaults = true,
                            seedOffset = GenerateSeedOffset(random, i, pIndex)
                        }
                        : CreateRandomPlanet(galaxy.id, pIndex, random));
                }

                if (galaxy.planets.Count > galaxy.planetCount)
                    galaxy.planets.RemoveRange(galaxy.planetCount, galaxy.planets.Count - galaxy.planetCount);

                for (int p = 0; p < galaxy.planets.Count; p++)
                {
                    PlanetDefinition_IO planet = galaxy.planets[p] ?? new PlanetDefinition_IO();
                    galaxy.planets[p] = planet;

                    if (string.IsNullOrEmpty(planet.id))
                        planet.id = galaxy.id + "_planet_" + p;

                    if (string.IsNullOrEmpty(planet.label))
                        planet.label = p == 0 && i == 0
                            ? (Find.World?.info?.name ?? "RimWorld")
                            : GeneratePlanetName(random);

                    if (planet.seedOffset == 0 && !(planet.startPlanet && p == 0 && i == 0))
                        planet.seedOffset = GenerateSeedOffset(random, i, p);

                    if (planet.startPlanet)
                    {
                        if (startPlanetAssigned)
                        {
                            planet.startPlanet = false;
                        }
                        else
                        {
                            startPlanetAssigned = true;
                            planet.id = "homeworld";
                            if (string.IsNullOrEmpty(planet.label))
                                planet.label = Find.World?.info?.name ?? "RimWorld";
                        }
                    }
                }
            }

            if (!startPlanetAssigned && config.galaxies.Count > 0 && config.galaxies[0].planets.Count > 0)
            {
                config.galaxies[0].planets[0].startPlanet = true;
                if (config.galaxies[0].planets[0].id != "homeworld")
                    config.galaxies[0].planets[0].id = "homeworld";
            }
        }

        public static PlanetDefinition_IO GetStartPlanet(GalaxyWorldConfiguration config)
        {
            if (config?.galaxies == null)
                return null;

            foreach (GalaxyDefinition galaxy in config.galaxies)
            {
                if (galaxy?.planets == null)
                    continue;

                PlanetDefinition_IO found = galaxy.planets.FirstOrDefault(p => p != null && p.startPlanet);
                if (found != null)
                    return found;
            }

            return config.galaxies.FirstOrDefault()?.planets?.FirstOrDefault();
        }

        public static GalaxyDefinition GetGalaxy(GalaxyWorldConfiguration config, string galaxyId)
        {
            return config?.galaxies?.FirstOrDefault(g => g != null && g.id == galaxyId);
        }

        private static PlanetDefinition_IO CreateRandomPlanet(string galaxyId, int planetIndex, Random random)
        {
            PlanetDefinition_IO planet = new PlanetDefinition_IO
            {
                id = galaxyId + "_planet_" + planetIndex,
                label = GeneratePlanetName(random),
                overallTemperature = 0.55f + (float)random.NextDouble() * 0.95f,
                overallRainfall = 0.35f + (float)random.NextDouble() * 1.15f,
                overallPopulation = 0.15f + (float)random.NextDouble() * 0.95f,
                coverage = 0.18f + (float)random.NextDouble() * 0.18f,
                pollution = (float)random.NextDouble() * 0.18f,
                seedOffset = GenerateSeedOffset(random, ExtractTrailingIndex(galaxyId), planetIndex)
            };

            if (random.NextDouble() < 0.33d)
                planet.useVanillaDefaults = false;

            return planet;
        }

        private static int GenerateSeedOffset(Random random, int galaxyIndex, int planetIndex)
        {
            return galaxyIndex * 100000 + planetIndex * 1000 + random.Next(1, 999);
        }

        private static string GeneratePlanetName(Random random)
        {
            string prefix = NamePrefixes[random.Next(NamePrefixes.Length)];
            string middle = NameMiddles[random.Next(NameMiddles.Length)];
            string suffix = NameSuffixes[random.Next(NameSuffixes.Length)];

            string result = prefix + middle + suffix;
            return result.Replace("  ", " ").Trim();
        }

        private static Random CreateRandom(int salt)
        {
            int tickPart = unchecked((int)DateTime.UtcNow.Ticks);
            int guidPart = Guid.NewGuid().GetHashCode();
            int seed = tickPart ^ guidPart ^ (salt * 397);
            return new Random(seed);
        }

        private static int ExtractTrailingIndex(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            int underscore = value.LastIndexOf('_');
            if (underscore >= 0 && underscore < value.Length - 1)
            {
                int parsed;
                if (int.TryParse(value.Substring(underscore + 1), out parsed))
                    return parsed;
            }

            return 0;
        }
    }
}
