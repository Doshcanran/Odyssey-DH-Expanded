using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class ShipThingSnapshot : IExposable
        {
            public Thing thing;
            public IntVec3 offset = IntVec3.Zero;

            public void ExposeData()
            {
                Scribe_Deep.Look(ref thing, "thing");
                Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);
            }
        }

        public class ShipPawnSnapshot : IExposable
        {
            public Pawn pawn;
            public IntVec3 offset = IntVec3.Zero;

            public void ExposeData()
            {
                Scribe_Deep.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);
            }
        }

        public class ShipTerrainSnapshot : IExposable
        {
            public IntVec3 offset = IntVec3.Zero;
            public TerrainDef terrainDef;

            public void ExposeData()
            {
                Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);
                Scribe_Defs.Look(ref terrainDef, "terrainDef");
            }
        }

        public class ShipRoofSnapshot : IExposable
        {
            public IntVec3 offset = IntVec3.Zero;
            public RoofDef roofDef;

            public void ExposeData()
            {
                Scribe_Values.Look(ref offset, "offset", IntVec3.Zero);
                Scribe_Defs.Look(ref roofDef, "roofDef");
            }
        }

        public class ShipSnapshot : IExposable
        {
            public int shipThingId;
            public string shipDefName;
            public string currentNodeId;
            public IntVec3 anchorCell = IntVec3.Zero;
            public List<ShipThingSnapshot> buildings = new List<ShipThingSnapshot>();
            public List<ShipThingSnapshot> items = new List<ShipThingSnapshot>();
            public List<ShipPawnSnapshot> pawns = new List<ShipPawnSnapshot>();
            public List<ShipTerrainSnapshot> terrains = new List<ShipTerrainSnapshot>();
            public List<ShipRoofSnapshot> roofs = new List<ShipRoofSnapshot>();

            public void ExposeData()
            {
                Scribe_Values.Look(ref shipThingId, "shipThingId", 0);
                Scribe_Values.Look(ref shipDefName, "shipDefName");
                Scribe_Values.Look(ref currentNodeId, "currentNodeId");
                Scribe_Values.Look(ref anchorCell, "anchorCell", IntVec3.Zero);
                Scribe_Collections.Look(ref buildings, "buildings", LookMode.Deep);
                Scribe_Collections.Look(ref items, "items", LookMode.Deep);
                Scribe_Collections.Look(ref pawns, "pawns", LookMode.Deep);
                Scribe_Collections.Look(ref terrains, "terrains", LookMode.Deep);
                Scribe_Collections.Look(ref roofs, "roofs", LookMode.Deep);

                if (buildings == null) buildings = new List<ShipThingSnapshot>();
                if (items == null) items = new List<ShipThingSnapshot>();
                if (pawns == null) pawns = new List<ShipPawnSnapshot>();
                if (terrains == null) terrains = new List<ShipTerrainSnapshot>();
                if (roofs == null) roofs = new List<ShipRoofSnapshot>();
            }
        }
}
