using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public class Window_TransitMonitor : Window
    {
        private readonly ShipTransitRecord record;
        private readonly WorldComponent_Interstellar data;

        public override Vector2 InitialSize => new Vector2(UI.screenWidth - 60f, UI.screenHeight - 80f);

        public Window_TransitMonitor(ShipTransitRecord record)
        {
            this.record = record;
            data = Find.World.GetComponent<WorldComponent_Interstellar>();
            forcePause = false;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            draggable = false;
            optionalTitle = "Монитор полёта";
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawSolarSystemUI(inRect, data, record, true, data.selectedGalaxyId);
        }

        public static void DrawSolarSystemUI(Rect inRect, WorldComponent_Interstellar data, ShipTransitRecord highlight, bool includeSidePanel, string galaxyId)
        {
            IEnumerable<OrbitalNode> visibleNodes = data.GetNodesForGalaxy(galaxyId).ToList();
            IEnumerable<ShipTransitRecord> visibleTravels = data.activeTravels.Where(t =>
            {
                if (t == null)
                    return false;

                OrbitalNode src = data.GetNodeById(t.sourceId);
                OrbitalNode dst = data.GetNodeById(t.destinationId);
                return (src != null && src.galaxyId == galaxyId) || (dst != null && dst.galaxyId == galaxyId);
            }).ToList();

            Rect canvasRect = includeSidePanel ? new Rect(inRect.x, inRect.y, inRect.width * 0.72f, inRect.height) : inRect;
            Rect sideRect = includeSidePanel ? new Rect(canvasRect.xMax + 12f, inRect.y, inRect.width - canvasRect.width - 12f, inRect.height) : Rect.zero;

            Widgets.DrawMenuSection(canvasRect);
            Rect inner = canvasRect.ContractedBy(10f);

            GUI.BeginGroup(inner);
            Vector2 center = new Vector2(inner.width / 2f, inner.height / 2f);
            float scale = Mathf.Min(inner.width, inner.height) / 520f;

            foreach (OrbitalNode node in visibleNodes)
                if (node.radius > 1f)
                    DrawOrbitRing(center, node.radius * scale);

            Widgets.DrawBoxSolid(new Rect(center.x - 18f, center.y - 18f, 36f, 36f), new Color(1f, 0.78f, 0.2f));

            foreach (OrbitalNode node in visibleNodes)
                DrawNode(node, center, scale, data);

            foreach (ShipTransitRecord travel in visibleTravels)
                DrawTravel(travel, center, scale, data, highlight);

            GUI.EndGroup();

            if (!includeSidePanel)
                return;

            Widgets.DrawMenuSection(sideRect);
            float curY = sideRect.y + 10f;
            Widgets.Label(new Rect(sideRect.x + 10f, curY, sideRect.width - 20f, 28f), "Маршруты");
            curY += 34f;

            foreach (ShipTransitRecord travel in visibleTravels)
            {
                Rect row = new Rect(sideRect.x + 10f, curY, sideRect.width - 20f, 112f);
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));
                string src = data.ResolveNodeLabel(data.GetNodeById(travel.sourceId));
                string dst = data.ResolveNodeLabel(data.GetNodeById(travel.destinationId));
                string routeLabel = (travel.shipLabel ?? "Корабль") + ": " + src + " → " + dst;
                if (travel.intergalacticTravel)
                    routeLabel += " [межгалактический]";
                Widgets.Label(new Rect(row.x + 8f, row.y + 6f, row.width - 16f, 24f), routeLabel);
                Widgets.Label(new Rect(row.x + 8f, row.y + 30f, row.width - 16f, 24f), "Прогресс: " + (travel.Progress * 100f).ToString("0") + "% | Событий: " + (travel.eventLog != null ? travel.eventLog.Count : 0));

                Rect progressRect = new Rect(row.x + 8f, row.y + 54f, row.width - 16f, 18f);
                Widgets.FillableBar(progressRect, travel.Progress);

                ShipTransitEvent latestEvent = travel.eventLog != null && travel.eventLog.Count > 0 ? travel.eventLog[travel.eventLog.Count - 1] : null;
                Widgets.Label(new Rect(row.x + 8f, row.y + 74f, row.width - 16f, 20f), latestEvent != null ? ("Последнее: " + latestEvent.title) : "Последнее: нет событий");
                if (latestEvent != null)
                    TooltipHandler.TipRegion(new Rect(row.x + 8f, row.y + 74f, row.width - 16f, 20f), ShipTransitEventUtility.DescribeConsequences(latestEvent));

                if (Widgets.ButtonText(new Rect(row.x + row.width - 118f, row.y + 88f, 110f, 22f), "Монитор"))
                    Find.WindowStack.Add(new Window_TransitMonitor(travel));

                if (travel.stage == InterstellarTransitStage.AwaitingLanding)
                {
                    if (Widgets.ButtonText(new Rect(row.x + 8f, row.y + 88f, 110f, 22f), "Посадка"))
                        Find.WindowStack.Add(new Window_ShipLanding(travel));
                }

                if (VoidMapUtility.HasVoidMap(travel))
                {
                    float boardBtnX = (travel.stage == InterstellarTransitStage.AwaitingLanding) ? row.x + 126f : row.x + 8f;
                    if (Widgets.ButtonText(new Rect(boardBtnX, row.y + 88f, 110f, 22f), "На борт"))
                    {
                        Map voidMap = VoidMapUtility.GetVoidMap(travel.voidMapTile);
                        if (voidMap != null)
                            Current.Game.CurrentMap = voidMap;
                    }
                }

                curY += 120f;
            }

            if (highlight != null)
            {
                float panelY = curY + 8f;
                Rect panel = new Rect(sideRect.x + 10f, panelY, sideRect.width - 20f, Mathf.Max(120f, sideRect.yMax - panelY - 10f));
                Widgets.DrawMenuSection(panel);
                Rect innerPanel = panel.ContractedBy(8f);

                Widgets.Label(new Rect(innerPanel.x, innerPanel.y, innerPanel.width, 24f), "Журнал событий");
                float eventY = innerPanel.y + 28f;

                if (highlight.eventLog == null || highlight.eventLog.Count == 0)
                {
                    Widgets.Label(new Rect(innerPanel.x, eventY, innerPanel.width, 24f), "Пока спокойно. Событий не было.");
                }
                else
                {
                    for (int i = highlight.eventLog.Count - 1; i >= 0 && eventY < innerPanel.yMax - 22f; i--)
                    {
                        ShipTransitEvent ev = highlight.eventLog[i];
                        string line = "• " + ev.title + " — " + ev.description + (!string.IsNullOrEmpty(ev.impactSummary) ? " [" + ev.impactSummary + "]" : string.Empty);
                        Widgets.Label(new Rect(innerPanel.x, eventY, innerPanel.width, 40f), line);
                        eventY += 38f;
                    }
                }
            }
        }

        private static void DrawOrbitRing(Vector2 center, float radius)
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

        private static void DrawNode(OrbitalNode node, Vector2 center, float scale, WorldComponent_Interstellar data)
        {
            Vector2 drawPos = center + OrbitalMath.Position(node) * scale;
            float size = 14f;
            Color color = Color.white;

            switch (node.type)
            {
                case OrbitalNodeType.Planet:
                    color = new Color(0.35f, 0.75f, 1f);
                    size = 18f;
                    break;
                case OrbitalNodeType.Station:
                    color = new Color(0.75f, 0.95f, 0.75f);
                    size = 14f;
                    break;
                case OrbitalNodeType.AsteroidBelt:
                    color = new Color(0.65f, 0.65f, 0.65f);
                    size = 12f;
                    break;
                case OrbitalNodeType.Asteroid:
                    color = new Color(0.75f, 0.72f, 0.62f);
                    size = 10f;
                    break;
            }

            Widgets.DrawBoxSolid(new Rect(drawPos.x - size / 2f, drawPos.y - size / 2f, size, size), color);
            Widgets.Label(new Rect(drawPos.x + size * 0.6f, drawPos.y - 12f, 120f, 24f), data.ResolveNodeLabel(node));
        }

        private static void DrawTravel(ShipTransitRecord travel, Vector2 center, float scale, WorldComponent_Interstellar data, ShipTransitRecord highlight)
        {
            OrbitalNode src = data.GetNodeById(travel.sourceId);
            OrbitalNode dst = data.GetNodeById(travel.destinationId);
            if (src == null || dst == null)
                return;

            Vector2 a = center + OrbitalMath.Position(src) * scale;
            Vector2 b = center + OrbitalMath.Position(dst) * scale;
            Color lineColor = highlight == travel ? Color.yellow : (travel.intergalacticTravel ? new Color(1f, 0.6f, 0.2f) : Color.cyan);
            Widgets.DrawLine(a, b, lineColor, highlight == travel ? 3f : 2f);

            Vector2 progressPos = Vector2.Lerp(a, b, travel.Progress);
            Widgets.DrawBoxSolid(new Rect(progressPos.x - 6f, progressPos.y - 6f, 12f, 12f), lineColor);
        }
    }
}
