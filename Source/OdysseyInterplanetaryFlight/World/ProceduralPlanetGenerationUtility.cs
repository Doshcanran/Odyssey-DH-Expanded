
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InterstellarOdyssey
{
    public static class ProceduralPlanetGenerationUtility
    {
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

        public static void RegenerateGalaxyPlanets(GalaxyDefinition galaxy, bool preserveExistingStartPlanet)
        {
            if (galaxy == null)
                return;

            int count = Mathf.Clamp(galaxy.planetCount, 1, 20);
            galaxy.hasPlanets = count > 0;

            int startIndex = 0;
            if (preserveExistingStartPlanet && galaxy.planets != null)
            {
                for (int i = 0; i < galaxy.planets.Count; i++)
                {
                    if (galaxy.planets[i] != null && galaxy.planets[i].startPlanet)
                    {
                        startIndex = Mathf.Clamp(i, 0, count - 1);
                        break;
                    }
                }
            }

            galaxy.planets.Clear();
            int galaxySeed = BuildGalaxySeed(galaxy);

            for (int i = 0; i < count; i++)
            {
                galaxy.planets.Add(CreatePlanet(galaxy, i, i == startIndex, galaxySeed));
            }
        }

        public static void RerollPlanet(GalaxyDefinition galaxy, PlanetDefinition_IO planet, int planetIndex, bool preserveIdentity)
        {
            if (galaxy == null || planet == null)
                return;

            int seed = BuildGalaxySeed(galaxy) ^ (planetIndex * 7919) ^ Environment.TickCount;
            System.Random random = new System.Random(seed);

            string existingId = planet.id;
            string existingLabel = planet.label;
            bool startPlanet = planet.startPlanet;
            PlanetArchiveData existingArchive = planet.archive;

            ApplyRandomPlanetStats(planet, random, startPlanet);

            planet.id = preserveIdentity && !string.IsNullOrEmpty(existingId)
                ? existingId
                : BuildPlanetId(galaxy, planetIndex, startPlanet);

            planet.label = preserveIdentity && !string.IsNullOrEmpty(existingLabel)
                ? existingLabel
                : GeneratePlanetName(random);

            planet.archive = existingArchive;
        }

        public static void ApplyPreset(PlanetDefinition_IO planet, int planetIndex, PlanetGenerationPreset preset)
        {
            if (planet == null)
                return;

            int seed = planet.seedOffset != 0
                ? planet.seedOffset
                : (planetIndex + 1) * 73471;

            System.Random random = new System.Random(seed ^ ((int)preset * 100003));

            switch (preset)
            {
                case PlanetGenerationPreset.Temperate:
                    planet.overallTemperature = RandRange(random, 0.85f, 1.15f);
                    planet.overallRainfall = RandRange(random, 0.80f, 1.20f);
                    planet.overallPopulation = RandRange(random, 0.85f, 1.20f);
                    planet.coverage = RandRange(random, 0.28f, 0.50f);
                    planet.pollution = RandRange(random, 0.00f, 0.18f);
                    break;

                case PlanetGenerationPreset.Cold:
                    planet.overallTemperature = RandRange(random, 0.05f, 0.55f);
                    planet.overallRainfall = RandRange(random, 0.20f, 1.10f);
                    planet.overallPopulation = RandRange(random, 0.15f, 0.95f);
                    planet.coverage = RandRange(random, 0.18f, 0.45f);
                    planet.pollution = RandRange(random, 0.00f, 0.12f);
                    break;

                case PlanetGenerationPreset.Hot:
                    planet.overallTemperature = RandRange(random, 1.35f, 2.00f);
                    planet.overallRainfall = RandRange(random, 0.00f, 0.85f);
                    planet.overallPopulation = RandRange(random, 0.20f, 1.05f);
                    planet.coverage = RandRange(random, 0.12f, 0.42f);
                    planet.pollution = RandRange(random, 0.00f, 0.20f);
                    break;

                case PlanetGenerationPreset.Toxic:
                    planet.overallTemperature = RandRange(random, 0.65f, 1.70f);
                    planet.overallRainfall = RandRange(random, 0.10f, 1.25f);
                    planet.overallPopulation = RandRange(random, 0.00f, 0.60f);
                    planet.coverage = RandRange(random, 0.10f, 0.35f);
                    planet.pollution = RandRange(random, 0.55f, 1.00f);
                    break;
            }

            planet.useVanillaDefaults = false;
        }

        public static PlanetDefinition_IO CreatePlanet(GalaxyDefinition galaxy, int planetIndex, bool isStartPlanet, int? customGalaxySeed = null)
        {
            int galaxySeed = customGalaxySeed ?? BuildGalaxySeed(galaxy);
            int planetSeed = galaxySeed ^ ((planetIndex + 1) * 104729);
            System.Random random = new System.Random(planetSeed);

            PlanetDefinition_IO planet = new PlanetDefinition_IO
            {
                id = BuildPlanetId(galaxy, planetIndex, isStartPlanet),
                label = GeneratePlanetName(random),
                startPlanet = isStartPlanet,
                seedOffset = random.Next(100000, 2000000000),
                useVanillaDefaults = isStartPlanet
            };

            ApplyRandomPlanetStats(planet, random, isStartPlanet);
            return planet;
        }

        private static void ApplyRandomPlanetStats(PlanetDefinition_IO planet, System.Random random, bool isStartPlanet)
        {
            PlanetGenerationPreset preset = RollPreset(random, isStartPlanet);
            ApplyPreset(planet, 0, preset);

            if (isStartPlanet)
            {
                planet.overallTemperature = Mathf.Clamp(planet.overallTemperature, 0.75f, 1.20f);
                planet.overallRainfall = Mathf.Clamp(planet.overallRainfall, 0.55f, 1.30f);
                planet.overallPopulation = Mathf.Clamp(planet.overallPopulation, 0.70f, 1.35f);
                planet.coverage = Mathf.Clamp(planet.coverage, 0.28f, 0.55f);
                planet.pollution = Mathf.Clamp(planet.pollution, 0.00f, 0.08f);
                planet.useVanillaDefaults = true;
            }
            else
            {
                planet.useVanillaDefaults = false;
            }
        }

        private static PlanetGenerationPreset RollPreset(System.Random random, bool isStartPlanet)
        {
            if (isStartPlanet)
                return PlanetGenerationPreset.Temperate;

            double roll = random.NextDouble();
            if (roll < 0.38) return PlanetGenerationPreset.Temperate;
            if (roll < 0.60) return PlanetGenerationPreset.Cold;
            if (roll < 0.84) return PlanetGenerationPreset.Hot;
            return PlanetGenerationPreset.Toxic;
        }

        private static string BuildPlanetId(GalaxyDefinition galaxy, int planetIndex, bool isStartPlanet)
        {
            if (isStartPlanet)
                return "homeworld";

            string galaxyId = string.IsNullOrEmpty(galaxy?.id) ? "galaxy" : galaxy.id;
            return galaxyId + "_planet_" + planetIndex;
        }

        private static int BuildGalaxySeed(GalaxyDefinition galaxy)
        {
            unchecked
            {
                int seed = 17;
                seed = seed * 31 + (galaxy?.id != null ? galaxy.id.GetHashCode() : 0);
                seed = seed * 31 + (galaxy?.label != null ? galaxy.label.GetHashCode() : 0);
                seed = seed * 31 + (galaxy?.solarSystemId != null ? galaxy.solarSystemId.GetHashCode() : 0);
                seed = seed * 31 + (galaxy != null ? galaxy.planetCount : 0);
                return seed;
            }
        }

        private static float RandRange(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static string GeneratePlanetName(System.Random random)
        {
            string name = NameStarts[random.Next(NameStarts.Length)]
                        + NameMids[random.Next(NameMids.Length)]
                        + NameEnds[random.Next(NameEnds.Length)];

            if (random.NextDouble() < 0.20)
                name += " " + random.Next(2, 9);

            return name;
        }
    }

    public enum PlanetGenerationPreset
    {
        Temperate = 0,
        Cold = 1,
        Hot = 2,
        Toxic = 3
    }
}
