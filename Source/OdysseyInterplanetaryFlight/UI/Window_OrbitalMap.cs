using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class Window_OrbitalMap : Window
        {
            private readonly Thing ship;
            private ShipValidationReport validationReport;
            private Vector2 scrollPos;

            private WorldComponent_Interstellar Data
            {
                get { return Find.World.GetComponent<WorldComponent_Interstellar>(); }
            }

            public override Vector2 InitialSize
            {
                get { return new Vector2(980f, 720f); }
            }

            public Window_OrbitalMap(Thing ship)
            {
                this.ship = ship;
                forcePause = true;
                doCloseX = true;
                doCloseButton = true;
                absorbInputAroundWindow = true;
                closeOnAccept = false;
                draggable = true;
                Data.GenerateIfNeeded();
                RefreshValidationReport();
            }

            private void RefreshValidationReport()
            {
                validationReport = ShipValidationUtility.ValidateForLaunch(ship);
            }

            public override void DoWindowContents(Rect inRect)
            {
                Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
                Text.Font = GameFont.Medium;
                Widgets.Label(titleRect, "Орбитальная карта");
                Text.Font = GameFont.Small;

                Rect topInfo = new Rect(inRect.x, titleRect.yMax + 10f, inRect.width - 170f, 30f);
                OrbitalNode current = Data.GetCurrentNodeForShip(ship);
                Widgets.Label(topInfo, "Текущая цель: " + Data.ResolveNodeLabel(current));

                Rect refreshButtonRect = new Rect(inRect.xMax - 150f, titleRect.yMax + 6f, 150f, 32f);
                if (Widgets.ButtonText(refreshButtonRect, "Обновить проверку"))
                    RefreshValidationReport();

                Rect leftRect = new Rect(inRect.x, topInfo.yMax + 10f, inRect.width * 0.58f, inRect.height - 95f);
                Rect rightRect = new Rect(leftRect.xMax + 12f, topInfo.yMax + 10f, inRect.width - leftRect.width - 12f, inRect.height - 95f);

                float checklistHeight = Mathf.Min(250f, rightRect.height * 0.42f);
                Rect checklistRect = new Rect(rightRect.x, rightRect.y, rightRect.width, checklistHeight);
                Rect destinationsRect = new Rect(rightRect.x, checklistRect.yMax + 10f, rightRect.width, rightRect.height - checklistHeight - 10f);

                DrawOrbitCanvas(leftRect);
                DrawValidationChecklist(checklistRect);
                DrawDestinationList(destinationsRect, current);

                if (Data.IsShipTravelling(ship))
                {
                    Rect bottomRect = new Rect(inRect.x, inRect.yMax - 28f, inRect.width, 28f);
                    Widgets.Label(bottomRect, "Этот корабль уже находится в перелёте.");
                }
            }

            private void DrawOrbitCanvas(Rect rect)
            {
                Widgets.DrawMenuSection(rect);

                Rect inner = rect.ContractedBy(10f);
                GUI.BeginGroup(inner);

                Vector2 center = new Vector2(inner.width / 2f, inner.height / 2f);
                float scale = Mathf.Min(inner.width, inner.height) / 520f;

                foreach (OrbitalNode node in Data.nodes)
                    if (node.radius > 1f)
                        DrawOrbitRing(center, node.radius * scale);

                Rect starRect = new Rect(center.x - 16f, center.y - 16f, 32f, 32f);
                Widgets.DrawBoxSolid(starRect, new Color(1f, 0.78f, 0.2f));

                foreach (OrbitalNode node in Data.nodes)
                    DrawNode(node, center, scale);

                GUI.EndGroup();
            }

            private void DrawOrbitRing(Vector2 center, float radius)
            {
                int segments = 72;
                Vector2 prev = center + new Vector2(radius, 0f);

                for (int i = 1; i <= segments; i++)
                {
                    float angle = (360f / segments) * i * Mathf.Deg2Rad;
                    Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    Widgets.DrawLine(prev, next, Color.gray, 1f);
                    prev = next;
                }
            }

            private void DrawNode(OrbitalNode node, Vector2 center, float scale)
            {
                Vector2 pos = OrbitalMath.Position(node) * scale;
                Vector2 drawPos = center + pos;

                float size = 14f;
                Color color = Color.white;

                switch (node.type)
                {
                    case OrbitalNodeType.Planet:
                        color = new Color(0.35f, 0.75f, 1f);
                        size = 16f;
                        break;
                    case OrbitalNodeType.Station:
                        color = new Color(0.8f, 0.8f, 0.85f);
                        size = 12f;
                        break;
                    case OrbitalNodeType.Asteroid:
                    case OrbitalNodeType.AsteroidBelt:
                        color = new Color(0.65f, 0.55f, 0.45f);
                        size = 12f;
                        break;
                }

                Rect nodeRect = new Rect(drawPos.x - size / 2f, drawPos.y - size / 2f, size, size);
                Widgets.DrawBoxSolid(nodeRect, color);

                Rect labelRect = new Rect(drawPos.x + 8f, drawPos.y - 10f, 180f, 24f);
                Widgets.Label(labelRect, Data.ResolveNodeLabel(node));
            }


            private void DrawValidationChecklist(Rect rect)
            {
                if (validationReport == null)
                    RefreshValidationReport();

                Widgets.DrawMenuSection(rect);

                Rect inner = rect.ContractedBy(10f);
                Rect header = new Rect(inner.x, inner.y, inner.width - 100f, 25f);
                Widgets.Label(header, "Предстартовая проверка");

                Rect stateRect = new Rect(inner.xMax - 100f, inner.y, 100f, 25f);
                Color prevColor = GUI.color;
                GUI.color = validationReport != null && validationReport.CanLaunch ? new Color(0.45f, 1f, 0.45f) : new Color(1f, 0.45f, 0.45f);
                Widgets.Label(stateRect, validationReport != null && validationReport.CanLaunch ? "ГОТОВ" : "НЕ ГОТОВ");
                GUI.color = prevColor;

                float y = header.yMax + 8f;
                if (validationReport == null)
                {
                    Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "Нет данных проверки.");
                    return;
                }

                for (int i = 0; i < validationReport.checks.Count; i++)
                {
                    ShipValidationCheck check = validationReport.checks[i];
                    Rect row = new Rect(inner.x, y, inner.width, 32f);
                    string marker = check.passed ? "☑" : "☒";
                    Color rowColor = check.passed ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f);

                    GUI.color = rowColor;
                    Widgets.Label(new Rect(row.x, row.y, 24f, 24f), marker);
                    GUI.color = Color.white;

                    string label = check.label;
                    if (!string.IsNullOrEmpty(check.details))
                        label += " — " + check.details;

                    Widgets.Label(new Rect(row.x + 24f, row.y, row.width - 24f, row.height), label);
                    y += 28f;
                }

                if (validationReport.errors.Count > 0)
                {
                    y += 4f;
                    Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "Ошибок: " + validationReport.errors.Count);
                }
            }

            private void DrawDestinationList(Rect rect, OrbitalNode current)
            {
                Widgets.DrawMenuSection(rect);

                Rect inner = rect.ContractedBy(10f);
                Rect header = new Rect(inner.x, inner.y, inner.width, 25f);
                Widgets.Label(header, "Пункты назначения");

                Rect viewRect = new Rect(inner.x, header.yMax + 8f, inner.width - 16f, Mathf.Max(240f, Data.nodes.Count * 94f));
                Rect outRect = new Rect(inner.x, header.yMax + 8f, inner.width, inner.height - 35f);

                Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

                float curY = viewRect.y;
                foreach (OrbitalNode node in Data.nodes)
                {
                    Rect row = new Rect(viewRect.x, curY, viewRect.width, 84f);
                    Widgets.DrawHighlightIfMouseover(row);
                    Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));

                    Rect labelRect = new Rect(row.x + 10f, row.y + 8f, row.width - 160f, 24f);
                    Widgets.Label(labelRect, Data.ResolveNodeLabel(node));

                    ShipPropulsionReport propulsion = ShipPropulsionUtility.Evaluate(ship, current, node);
                    float days = propulsion.travelDays > 0f ? propulsion.travelDays : Mathf.Max(0.25f, OrbitalMath.Distance(current, node) / 45f);

                    Rect timeRect = new Rect(row.x + 10f, row.y + 30f, row.width - 160f, 24f);
                    Widgets.Label(timeRect, "Время: " + days.ToString("0.0") + " д. | Топливо: " + propulsion.fuelNeeded.ToString("0.#") + "/" + propulsion.totalFuel.ToString("0.#"));

                    Rect statsRect = new Rect(row.x + 10f, row.y + 48f, row.width - 160f, 24f);
                    Widgets.Label(statsRect, "Масса: " + propulsion.shipMass.ToString("0.#") + " | Тяга: " + propulsion.thrust.ToString("0.#") + " | Дальность: " + propulsion.maxRange.ToString("0.#"));

                    Rect buttonRect = new Rect(row.xMax - 130f, row.y + 15f, 120f, 30f);
                    bool disabled = Data.IsShipTravelling(ship) || (current != null && current.id == node.id) || !propulsion.hasEnoughFuel || !propulsion.hasEnoughThrust;

                    if (disabled)
                        GUI.color = Color.gray;

                    if (Widgets.ButtonText(buttonRect, "Лететь") && !disabled)
                    {
                        RefreshValidationReport();
                        if (Data.StartTravel(ship, node))
                            Close();
                    }

                    if (!propulsion.hasEnoughFuel || !propulsion.hasEnoughThrust)
                    {
                        GUI.color = new Color(1f, 0.55f, 0.55f);
                        Widgets.Label(new Rect(row.xMax - 170f, row.y + 50f, 160f, 24f), propulsion.blockingReason ?? "Маршрут недоступен");
                    }

                    GUI.color = Color.white;
                    curY += 92f;
                }

                Widgets.EndScrollView();
            }
        }
}
