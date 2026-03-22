using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public class Window_OrbitalMap : Window
    {
        private readonly Thing ship;
        private ShipValidationReport validationReport;
        private Vector2 destinationScrollPos;
        private Vector2 checklistScrollPos;

        private WorldComponent_Interstellar Data => Find.World.GetComponent<WorldComponent_Interstellar>();

        public override Vector2 InitialSize => new Vector2(1180f, 790f);

        public Window_OrbitalMap(Thing ship)
        {
            this.ship = ship;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            draggable = true;
            optionalTitle = "IO_RefreshOrbitalMap".Translate();
            Data.GenerateIfNeeded();

            OrbitalNode currentNode = Data.GetCurrentNodeForShip(ship);
            if (currentNode != null)
                Data.SelectGalaxy(currentNode.galaxyId);

            RefreshValidationReport();
        }

        private void RefreshValidationReport()
        {
            validationReport = ShipValidationUtility.ValidateForLaunch(ship);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect refreshButtonRect = new Rect(inRect.xMax - 160f, inRect.y + 4f, 160f, 32f);
            if (Widgets.ButtonText(refreshButtonRect, "IO_RefreshCheck".Translate()))
                RefreshValidationReport();

            Rect galaxyButtonsRect = new Rect(inRect.x, inRect.y + 40f, inRect.width - 180f, 80f);
            float galaxyButtonsBottom = GalaxyUiUtility.DrawGalaxyTabs(galaxyButtonsRect, Data);

            OrbitalNode current = Data.GetCurrentNodeForShip(ship);
            string infoText = "IO_CurrentTarget".Translate(Data.ResolveNodeLabel(current), Data.GetGalaxyById(Data.selectedGalaxyId)?.label ?? Data.selectedGalaxyId);
            float infoHeight = Text.CalcHeight(infoText, inRect.width);
            Rect infoRect = new Rect(inRect.x, galaxyButtonsBottom + 8f, inRect.width, infoHeight);
            Widgets.Label(infoRect, infoText);

            float statusHeight = Data.IsShipTravelling(ship) ? 24f : 0f;
            Rect contentRect = new Rect(inRect.x, infoRect.yMax + 8f, inRect.width, inRect.height - (infoRect.yMax - inRect.y) - 8f - statusHeight);
            Rect leftRect = new Rect(contentRect.x, contentRect.y, Mathf.Floor(contentRect.width * 0.58f), contentRect.height);
            Rect rightRect = new Rect(leftRect.xMax + 12f, contentRect.y, contentRect.width - leftRect.width - 12f, contentRect.height);

            float checklistHeight = Mathf.Clamp(rightRect.height * 0.40f, 220f, 320f);
            Rect checklistRect = new Rect(rightRect.x, rightRect.y, rightRect.width, checklistHeight);
            Rect destinationsRect = new Rect(rightRect.x, checklistRect.yMax + 10f, rightRect.width, rightRect.height - checklistHeight - 10f);

            DrawOrbitCanvas(leftRect);
            DrawValidationChecklist(checklistRect);
            DrawDestinationList(destinationsRect, current);

            if (Data.IsShipTravelling(ship))
            {
                Rect statusRect = new Rect(inRect.x, contentRect.yMax + 6f, inRect.width, 24f);
                Widgets.Label(statusRect, "IO_ShipAlreadyInTransit".Translate());
            }
        }

        private void DrawOrbitCanvas(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect inner = rect.ContractedBy(10f);
            GUI.BeginGroup(inner);

            Vector2 center = new Vector2(inner.width / 2f, inner.height / 2f);
            float scale = Mathf.Min(inner.width, inner.height) / 520f;
            IEnumerable<OrbitalNode> visibleNodes = Data.GetNodesForSelectedGalaxy().ToList();

            foreach (OrbitalNode node in visibleNodes)
                if (node.radius > 1f)
                    DrawOrbitRing(center, node.radius * scale);

            Rect starRect = new Rect(center.x - 16f, center.y - 16f, 32f, 32f);
            Widgets.DrawBoxSolid(starRect, new Color(1f, 0.78f, 0.2f));

            foreach (OrbitalNode node in visibleNodes)
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
                    size = 18f;
                    break;
                case OrbitalNodeType.Station:
                    color = new Color(0.7f, 1f, 0.7f);
                    size = 14f;
                    break;
                case OrbitalNodeType.AsteroidBelt:
                    color = new Color(0.7f, 0.7f, 0.7f);
                    size = 12f;
                    break;
                case OrbitalNodeType.Asteroid:
                    color = new Color(0.7f, 0.65f, 0.55f);
                    size = 10f;
                    break;
            }

            Rect nodeRect = new Rect(drawPos.x - size / 2f, drawPos.y - size / 2f, size, size);
            Widgets.DrawBoxSolid(nodeRect, color);
            Widgets.Label(new Rect(nodeRect.xMax + 4f, nodeRect.y - 6f, 150f, 24f), Data.ResolveNodeLabel(node));
        }

        private void DrawValidationChecklist(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);

            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "IO_ShipCheck".Translate());

            if (validationReport == null)
            {
                Widgets.Label(new Rect(inner.x, inner.y + 28f, inner.width, 24f), "IO_NoData".Translate());
                return;
            }

            string text = validationReport.ToUserText();
            Rect outRect = new Rect(inner.x, inner.y + 28f, inner.width, inner.height - 28f);
            float textHeight = Text.CalcHeight(text, outRect.width - 16f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height - 4f, textHeight + 6f));

            Widgets.BeginScrollView(outRect, ref checklistScrollPos, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, viewRect.height), text);
            Widgets.EndScrollView();
        }

        private void DrawDestinationList(Rect rect, OrbitalNode current)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "IO_AvailableRoutes".Translate());

            string currentNodeId = Data.GetCurrentNodeIdForShip(ship);
            Log.Message("[IO:OrbitalMap] DrawDestinationList: ship=" + (ship?.LabelCap ?? "null")
                + " thingId=" + (ship?.thingIDNumber.ToString() ?? "?")
                + " currentNodeId=" + currentNodeId
                + " currentPlanetNodeId=" + Data.currentPlanetNodeId
                + " totalNodes=" + Data.nodes.Count);
            var destinations = Data.nodes.Where(n => n != null && n.id != currentNodeId).OrderBy(n => n.galaxyId).ThenBy(n => n.radius).ToList();
            Rect outRect = new Rect(inner.x, inner.y + 28f, inner.width, inner.height - 28f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height - 4f, destinations.Count * 82f + 8f));

            Widgets.BeginScrollView(outRect, ref destinationScrollPos, viewRect);
            float curY = 0f;

            foreach (OrbitalNode node in destinations)
            {
                Rect row = new Rect(0f, curY, viewRect.width, 76f);
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));

                bool intergalactic = current != null && !string.Equals(current.galaxyId, node.galaxyId);
                string routeLabel = "IO_TravelTo".Translate(Data.ResolveNodeLabel(node), Data.GetGalaxyById(node.galaxyId)?.label ?? node.galaxyId);
                string travelLabel = intergalactic ? "IO_IntergalacticTestJump".Translate() : "IO_IntrasystemRoute".Translate();

                Widgets.Label(new Rect(row.x + 8f, row.y + 8f, row.width - 150f, 22f), routeLabel);
                Widgets.Label(new Rect(row.x + 8f, row.y + 34f, row.width - 150f, 20f), travelLabel);

                if (Widgets.ButtonText(new Rect(row.width - 130f, row.y + 22f, 120f, 28f), "IO_Fly".Translate()))
                {
                    if (Data.StartTravel(ship, node))
                        Close();
                }

                curY += 82f;
            }

            if (destinations.Count == 0)
                Widgets.Label(new Rect(0f, 0f, viewRect.width, 24f), "IO_NoTravelNodesFound".Translate());

            Widgets.EndScrollView();
        }
    }
}
