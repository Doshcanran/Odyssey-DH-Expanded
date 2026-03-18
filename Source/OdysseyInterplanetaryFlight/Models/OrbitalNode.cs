using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class OrbitalNode : IExposable
        {
            public string id;
            public string label;
            public OrbitalNodeType type;
            public float angle;
            public float radius;
            public int generatedTile = -1;
            public string generatedWorldObjectLabel;

            public OrbitalNode()
            {
            }

            public OrbitalNode(string id, string label, OrbitalNodeType type, float angle, float radius)
            {
                this.id = id;
                this.label = label;
                this.type = type;
                this.angle = angle;
                this.radius = radius;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref id, "id");
                Scribe_Values.Look(ref label, "label");
                Scribe_Values.Look(ref type, "type", OrbitalNodeType.Planet);
                Scribe_Values.Look(ref angle, "angle", 0f);
                Scribe_Values.Look(ref radius, "radius", 0f);
                Scribe_Values.Look(ref generatedTile, "generatedTile", -1);
                Scribe_Values.Look(ref generatedWorldObjectLabel, "generatedWorldObjectLabel");
            }
        }
}
