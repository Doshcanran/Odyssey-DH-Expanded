using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public static class OrbitalMath
        {
            public static Vector2 Position(OrbitalNode node)
            {
                if (node == null)
                    return Vector2.zero;

                float radians = node.angle * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * node.radius;
            }

            public static float Distance(OrbitalNode a, OrbitalNode b)
            {
                return Vector2.Distance(Position(a), Position(b));
            }
        }
}
