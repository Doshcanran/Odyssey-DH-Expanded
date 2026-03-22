using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace InterstellarOdyssey
{
    public static class GalaxyConfigUtility
    {
        private static readonly System.Random NameRandom = new System.Random(Guid.NewGuid().GetHashCode());

        private static readonly string[] NameStarts =
        {
            "Ar", "Bel", "Cer", "Dor", "Ere", "Fal", "Gal", "Hel", "Ira", "Jan",
            "Kor", "Lun", "Mor", "Ner", "Or", "Pra", "Qua", "Rhe", "Sol", "Tor",
            "Umb", "Val", "Wey", "Xan", "Yor", "Zen"
        };

        private static readonly string[] NameMids =
        {
            "a", "e", "i", "o", "u", "ae", "ia", "io", "ora", "ara", "eri", "una"
        };

        private static readonly string[] NameEnds =
        {
            "ron", "tis", "lia", "nus", "th", "ria", "mon", "car", "dra", "vek",
            "lon", "mir", "tan", "phos", "vii", "prime"
        };

        private static string GeneratePlanetName(HashSet<string> usedNames = null)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                string name = NameStarts[NameRandom.Next(NameStarts.Length)]
                            + NameMids[NameRandom.Next(NameMids.Length)]
                            + NameEnds[NameRandom.Next(NameEnds.Length)];

                if (NameRandom.NextDouble() < 0.22)
                    name += " " + (NameRandom.Next(2, 9)).ToString();

                if (usedNames == null || usedNames.Add(name))
                    return name;
            }

            string fallback = "Nova-" + NameRandom.Next(1000, 9999);
            if (usedNames != null)
                usedNames.Add(fallback);
            return fallback;
        }

        private static int GenerateSeedOffset(HashSet<int> usedSeeds = null)
        {
            for (int attempt = 0; attempt < 128; attempt++)
            {
                int seed = NameRandom.Next(100000, 2000000000);
                if (usedSeeds == null || usedSeeds.Add(seed))
                    return seed;
            }

            int fallback = Environment.TickCount ^ NameRandom.Next();
            if (usedSeeds != null)
                usedSeeds.Add(fallback);
            return fallback;
        }

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
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> usedSeeds = new HashSet<int>();

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
                label = GeneratePlanetName(usedNames),
                startPlanet = withStartPlanet,
                useVanillaDefaults = true,
                seedOffset = GenerateSeedOffset(usedSeeds)
            });
            galaxy.planets.Add(new PlanetDefinition_IO
            {
                id = galaxy.id + "_planet_1",
                label = GeneratePlanetName(usedNames),
                overallTemperature = 1.25f,
                overallRainfall = 0.65f,
                overallPopulation = 0.55f,
                coverage = 0.22f,
                seedOffset = GenerateSeedOffset(usedSeeds)
            });
            galaxy.planets.Add(new PlanetDefinition_IO
            {
                id = galaxy.id + "_planet_2",
                label = GeneratePlanetName(usedNames),
                overallTemperature = 0.55f,
                overallRainfall = 1.1f,
                overallPopulation = 0.35f,
                coverage = 0.28f,
                seedOffset = GenerateSeedOffset(usedSeeds)
            });

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
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> usedSeeds = new HashSet<int>();

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

                while (galaxy.planets.Count < galaxy.planetCount)
                {
                    int pIndex = galaxy.planets.Count;
                    galaxy.planets.Add(new PlanetDefinition_IO
                    {
                        id = galaxy.id + "_planet_" + pIndex,
                        label = GeneratePlanetName(usedNames),
                        seedOffset = GenerateSeedOffset(usedSeeds)
                    });
                }

                if (galaxy.planets.Count > galaxy.planetCount)
                    galaxy.planets.RemoveRange(galaxy.planetCount, galaxy.planets.Count - galaxy.planetCount);

                for (int p = 0; p < galaxy.planets.Count; p++)
                {
                    PlanetDefinition_IO planet = galaxy.planets[p] ?? new PlanetDefinition_IO();
                    galaxy.planets[p] = planet;

                    if (string.IsNullOrEmpty(planet.id))
                        planet.id = galaxy.id + "_planet_" + p;
                    if (string.IsNullOrEmpty(planet.label) || planet.label.StartsWith("Планета ", StringComparison.OrdinalIgnoreCase) || planet.label == "Ares" || planet.label == "Nivalis" || planet.label == (Find.World?.info?.name ?? "RimWorld"))
                        planet.label = GeneratePlanetName(usedNames);
                    else
                        usedNames.Add(planet.label);

                    if (planet.seedOffset == 0 || !usedSeeds.Add(planet.seedOffset))
                        planet.seedOffset = GenerateSeedOffset(usedSeeds);

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
    }
}
