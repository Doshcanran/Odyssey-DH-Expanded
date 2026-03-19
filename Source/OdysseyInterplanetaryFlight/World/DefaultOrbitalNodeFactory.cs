using System.Collections.Generic;
using Verse;

namespace InterstellarOdyssey
{
    public static class DefaultOrbitalNodeFactory
    {
        public static List<OrbitalNode> CreateDefaultNodes()
        {
            string planetName = Find.World != null && Find.World.info != null
                ? Find.World.info.name
                : "RimWorld";

            return new List<OrbitalNode>
            {
                new OrbitalNode("homeworld", planetName, OrbitalNodeType.Planet, 0f, 75f)
                {
                    galaxyId = "galaxy_0",
                    solarSystemId = "system_0",
                    planetId = "homeworld",
                    isStartSystem = true,
                    archivedForTravel = true
                },
                new OrbitalNode("ares", "Ares", OrbitalNodeType.Planet, 160f, 130f)
                {
                    galaxyId = "galaxy_0",
                    solarSystemId = "system_0",
                    planetId = "ares",
                    archivedForTravel = true
                },
                new OrbitalNode("nivalis", "Nivalis", OrbitalNodeType.Planet, 320f, 185f)
                {
                    galaxyId = "galaxy_0",
                    solarSystemId = "system_0",
                    planetId = "nivalis",
                    archivedForTravel = true
                },
                new OrbitalNode("station", "Орбитальная станция", OrbitalNodeType.Station, 75f, 45f)
                {
                    galaxyId = "galaxy_0",
                    solarSystemId = "system_0"
                },
                new OrbitalNode("belt", "Пояс астероидов", OrbitalNodeType.AsteroidBelt, 235f, 230f)
                {
                    galaxyId = "galaxy_0",
                    solarSystemId = "system_0"
                },
                new OrbitalNode("hekate", "Геката", OrbitalNodeType.Asteroid, 25f, 205f)
                {
                    galaxyId = "galaxy_0",
                    solarSystemId = "system_0"
                }
            };
        }
    }
}
