using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public static class GalaxyOrbitalNodeFactory
    {
        public static List<OrbitalNode> CreateNodes(GalaxyWorldConfiguration config)
        {
            List<OrbitalNode> result = new List<OrbitalNode>();
            if (config?.galaxies == null)
                return result;

            foreach (GalaxyDefinition galaxy in config.galaxies)
            {
                if (galaxy == null)
                    continue;

                string solarSystemId = string.IsNullOrEmpty(galaxy.solarSystemId) ? ("system_" + galaxy.id) : galaxy.solarSystemId;

                if (galaxy.hasPlanets && galaxy.planets != null)
                {
                    int count = Mathf.Max(1, galaxy.planets.Count);
                    for (int i = 0; i < galaxy.planets.Count; i++)
                    {
                        PlanetDefinition_IO planet = galaxy.planets[i];
                        if (planet == null)
                            continue;

                        result.Add(new OrbitalNode
                        {
                            id = planet.id,
                            label = planet.label,
                            type = OrbitalNodeType.Planet,
                            angle = (360f / count) * i,
                            radius = 85f + 42f * i,
                            galaxyId = galaxy.id,
                            solarSystemId = solarSystemId,
                            planetId = planet.id,
                            archivedForTravel = true,
                            isStartSystem = planet.startPlanet
                        });
                    }
                }

                if (galaxy.hasStations)
                {
                    for (int i = 0; i < galaxy.stationCount; i++)
                    {
                        result.Add(new OrbitalNode
                        {
                            id = galaxy.id + "_station_" + i,
                            label = "Станция " + (i + 1),
                            type = OrbitalNodeType.Station,
                            angle = 45f + i * 35f,
                            radius = 42f + i * 12f,
                            galaxyId = galaxy.id,
                            solarSystemId = solarSystemId
                        });
                    }
                }

                if (galaxy.hasAsteroidBelts)
                {
                    for (int i = 0; i < galaxy.beltCount; i++)
                    {
                        result.Add(new OrbitalNode
                        {
                            id = galaxy.id + "_belt_" + i,
                            label = "Пояс астероидов " + (i + 1),
                            type = OrbitalNodeType.AsteroidBelt,
                            angle = 165f + i * 14f,
                            radius = 220f + i * 26f,
                            galaxyId = galaxy.id,
                            solarSystemId = solarSystemId
                        });

                        result.Add(new OrbitalNode
                        {
                            id = galaxy.id + "_asteroid_" + i,
                            label = "Астероид " + (i + 1),
                            type = OrbitalNodeType.Asteroid,
                            angle = 185f + i * 18f,
                            radius = 205f + i * 20f,
                            galaxyId = galaxy.id,
                            solarSystemId = solarSystemId
                        });
                    }
                }
            }

            return result;
        }
    }
}
