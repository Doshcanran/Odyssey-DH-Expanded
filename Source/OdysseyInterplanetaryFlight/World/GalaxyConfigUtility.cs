using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace InterstellarOdyssey
{
    public static class GalaxyConfigUtility
    {
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
                label = worldName,
                startPlanet = withStartPlanet,
                useVanillaDefaults = true,
                seedOffset = index * 100
            });
            galaxy.planets.Add(new PlanetDefinition_IO
            {
                id = galaxy.id + "_planet_1",
                label = "Ares",
                overallTemperature = 1.25f,
                overallRainfall = 0.65f,
                overallPopulation = 0.55f,
                coverage = 0.22f,
                seedOffset = index * 100 + 1
            });
            galaxy.planets.Add(new PlanetDefinition_IO
            {
                id = galaxy.id + "_planet_2",
                label = "Nivalis",
                overallTemperature = 0.55f,
                overallRainfall = 1.1f,
                overallPopulation = 0.35f,
                coverage = 0.28f,
                seedOffset = index * 100 + 2
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
                        label = "Планета " + (pIndex + 1),
                        seedOffset = i * 100 + pIndex
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
                    if (string.IsNullOrEmpty(planet.label))
                        planet.label = p == 0 && i == 0 ? (Find.World?.info?.name ?? "RimWorld") : "Планета " + (p + 1);

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
