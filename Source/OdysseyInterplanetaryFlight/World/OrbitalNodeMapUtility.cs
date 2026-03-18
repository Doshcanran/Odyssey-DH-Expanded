using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public static class OrbitalNodeMapUtility
        {
            public static Map ResolveOrCreateMapForNode(OrbitalNode node)
            {
                if (node == null)
                    return Find.AnyPlayerHomeMap ?? Find.Maps.FirstOrDefault();

                if (node.id == "homeworld")
                    return Find.AnyPlayerHomeMap ?? Find.Maps.FirstOrDefault();

                if (node.generatedTile >= 0)
                {
                    Map existing = Find.Maps.FirstOrDefault(m => m != null && m.Parent != null && m.Parent.Tile == node.generatedTile);
                    if (existing != null)
                        return existing;
                }

                return CreateGeneratedMap(node);
            }

            private static Map CreateGeneratedMap(OrbitalNode node)
            {
                int tile = FindTileForNode(node);
                node.generatedTile = tile;

                MapParent parent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                parent.Tile = tile;
                parent.SetFaction(Faction.OfPlayer);
                node.generatedWorldObjectLabel = node.label;

                bool hasWorldObjectAtTile = Find.WorldObjects.AllWorldObjects.Any(o => o != null && o.Tile == tile);
                if (!hasWorldObjectAtTile)
                    Find.WorldObjects.Add(parent);

                IntVec3 size = GetMapSizeForNode(node);
                MapGeneratorDef genDef = ResolveMapGeneratorDef(node);
                return MapGenerator.GenerateMap(size, parent, genDef);
            }

            private static IntVec3 GetMapSizeForNode(OrbitalNode node)
            {
                switch (node.type)
                {
                    case OrbitalNodeType.Station:
                        return new IntVec3(140, 1, 140);
                    case OrbitalNodeType.Asteroid:
                    case OrbitalNodeType.AsteroidBelt:
                        return new IntVec3(180, 1, 180);
                    default:
                        return new IntVec3(220, 1, 220);
                }
            }

            private static MapGeneratorDef ResolveMapGeneratorDef(OrbitalNode node)
            {
                MapGeneratorDef def =
                    DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Player") ??
                    DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Faction") ??
                    DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();

                return def;
            }

            private static int FindTileForNode(OrbitalNode node)
            {
                Map home = Find.AnyPlayerHomeMap ?? Find.Maps.FirstOrDefault();
                int baseTile = home != null && home.Parent != null ? home.Parent.Tile : 0;
                int tileCount = Find.WorldGrid.TilesCount;
                int offset = Mathf.Abs((node.id ?? "node").GetHashCode());
                return (baseTile + 17 + (offset % Mathf.Max(1, tileCount - 1))) % Mathf.Max(1, tileCount);
            }
        }
}
