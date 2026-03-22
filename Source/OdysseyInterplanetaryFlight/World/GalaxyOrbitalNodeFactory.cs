using System;
using System.Collections.Generic;
using UnityEngine;

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

                string solarSystemId = string.IsNullOrEmpty(galaxy.solarSystemId)
                    ? ("system_" + galaxy.id)
                    : galaxy.solarSystemId;

                if (!galaxy.hasPlanets || galaxy.planets == null || galaxy.planets.Count == 0)
                    continue;

                int count = Mathf.Max(1, galaxy.planets.Count);
                float[] randomAngles = BuildPlanetAngles(galaxy);

                for (int i = 0; i < galaxy.planets.Count; i++)
                {
                    PlanetDefinition_IO planet = galaxy.planets[i];
                    if (planet == null)
                        continue;

                    float radius = 85f + 42f * i;
                    if (planet.startPlanet)
                        radius = 85f;

                    result.Add(new OrbitalNode
                    {
                        id = planet.id,
                        label = planet.label,
                        type = OrbitalNodeType.Planet,
                        angle = i < randomAngles.Length ? randomAngles[i] : (360f / count) * i,
                        radius = radius,
                        galaxyId = galaxy.id,
                        solarSystemId = solarSystemId,
                        planetId = planet.id,
                        archivedForTravel = true,
                        isStartSystem = planet.startPlanet
                    });
                }
            }

            return result;
        }

        private static float[] BuildPlanetAngles(GalaxyDefinition galaxy)
        {
            if (galaxy == null || galaxy.planets == null || galaxy.planets.Count == 0)
                return new float[0];

            int count = galaxy.planets.Count;
            float[] angles = new float[count];
            float step = 360f / Mathf.Max(1, count);
            int seed = BuildGalaxySeed(galaxy);
            System.Random random = new System.Random(seed);
            float offset = (float)(random.NextDouble() * 360.0);

            for (int i = 0; i < count; i++)
            {
                float jitter = (float)(random.NextDouble() * (step * 0.55f) - (step * 0.275f));
                angles[i] = NormalizeAngle(offset + i * step + jitter);
            }

            Array.Sort(angles);
            return angles;
        }

        private static int BuildGalaxySeed(GalaxyDefinition galaxy)
        {
            unchecked
            {
                int seed = 17;
                seed = seed * 31 + (galaxy.id != null ? galaxy.id.GetHashCode() : 0);
                seed = seed * 31 + (galaxy.solarSystemId != null ? galaxy.solarSystemId.GetHashCode() : 0);

                if (galaxy.planets != null)
                {
                    for (int i = 0; i < galaxy.planets.Count; i++)
                    {
                        PlanetDefinition_IO planet = galaxy.planets[i];
                        if (planet == null)
                            continue;

                        seed = seed * 31 + planet.seedOffset;
                        seed = seed * 31 + (planet.id != null ? planet.id.GetHashCode() : i);
                    }
                }

                return seed;
            }
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle < 0f)
                angle += 360f;
            while (angle >= 360f)
                angle -= 360f;
            return angle;
        }
    }
}
