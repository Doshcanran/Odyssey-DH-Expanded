using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class ShipClusterData
        {
            public Map map;
            public Faction faction;
            public Thing anchor;
            public IntVec3 anchorCell = IntVec3.Zero;
            public HashSet<IntVec3> terrainCells = new HashSet<IntVec3>();
            public List<Thing> structuralThings = new List<Thing>();
            public HashSet<IntVec3> occupancyCells = new HashSet<IntVec3>();
            public List<Thing> allBuildings = new List<Thing>();
            public List<Pawn> pawns = new List<Pawn>();
            public List<Thing> items = new List<Thing>();
        }

        public class ShipValidationCheck
        {
            public string label;
            public bool passed;
            public bool warningOnly;
            public string details;

            public ShipValidationCheck()
            {
            }

            public ShipValidationCheck(string label, bool passed, string details = null, bool warningOnly = false)
            {
                this.label = label;
                this.passed = passed;
                this.details = details;
                this.warningOnly = warningOnly;
            }
        }

        public class ShipValidationReport
        {
            public readonly List<string> errors = new List<string>();
            public readonly List<string> warnings = new List<string>();
            public readonly List<ShipValidationCheck> checks = new List<ShipValidationCheck>();

            public bool CanLaunch => errors.Count == 0;

            public void AddCheck(string label, bool passed, string details = null, bool warningOnly = false)
            {
                checks.Add(new ShipValidationCheck(label, passed, details, warningOnly));
            }

            public void Error(string text)
            {
                if (!string.IsNullOrEmpty(text))
                    errors.Add(text);
            }

            public void Warning(string text)
            {
                if (!string.IsNullOrEmpty(text))
                    warnings.Add(text);
            }

            public string ToUserText()
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("Предстартовая проверка корабля");

                if (checks.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Чеклист:");
                    for (int i = 0; i < checks.Count; i++)
                    {
                        ShipValidationCheck check = checks[i];
                        string marker = check.passed ? "[OK]" : (check.warningOnly ? "[!]" : "[X]");
                        sb.AppendLine(marker + " " + check.label + (string.IsNullOrEmpty(check.details) ? string.Empty : ": " + check.details));
                    }
                }

                if (errors.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Ошибки:");
                    for (int i = 0; i < errors.Count; i++)
                        sb.AppendLine("• " + errors[i]);
                }

                if (warnings.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Предупреждения:");
                    for (int i = 0; i < warnings.Count; i++)
                        sb.AppendLine("• " + warnings[i]);
                }

                if (errors.Count == 0 && warnings.Count == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Корабль готов к старту.");
                }

                return sb.ToString().TrimEnd();
            }
        }
}
