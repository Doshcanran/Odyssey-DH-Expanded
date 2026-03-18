using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public class Window_ShipLanding : Window
    {
        private readonly ShipTransitRecord travel;
        private readonly WorldComponent_Interstellar data;
        private Vector2 scrollPos;
        private ShipLandingMode selectedMode;

        public override Vector2 InitialSize => new Vector2(840f, 690f);

        public Window_ShipLanding(ShipTransitRecord travel)
        {
            this.travel = travel;
            data = Find.World.GetComponent<WorldComponent_Interstellar>();
            selectedMode = travel != null ? travel.preferredLandingMode : ShipLandingMode.Precise;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "Посадка корабля");

            OrbitalNode destination = travel != null ? data.GetNodeById(travel.destinationId) : null;
            Widgets.Label(new Rect(inRect.x, inRect.y + 32f, inRect.width, 24f), "Цель: " + data.ResolveNodeLabel(destination));

            Rect modeRect = new Rect(inRect.x, inRect.y + 62f, inRect.width, 170f);
            DrawModeSelector(modeRect, destination);

            List<Map> maps = BuildLandingMapList(destination);
            Rect outRect = new Rect(inRect.x, modeRect.yMax + 8f, inRect.width, inRect.height - (modeRect.yMax - inRect.y) - 8f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(120f, maps.Count * 78f));

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float curY = 0f;

            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                Rect row = new Rect(0f, curY, viewRect.width, 70f);
                Widgets.DrawMenuSection(row);

                string mapLabel = map.Parent != null ? map.Parent.LabelCap : "Карта";
                Widgets.Label(new Rect(row.x + 10f, row.y + 8f, row.width - 180f, 24f), mapLabel);
                Widgets.Label(new Rect(row.x + 10f, row.y + 30f, row.width - 180f, 24f), "Размер: " + map.Size.x + "x" + map.Size.z + " | Режим: " + data.ResolveLandingModeLabel(selectedMode));

                if (Widgets.ButtonText(new Rect(row.width - 150f, row.y + 20f, 140f, 30f), "Посадить"))
                {
                    if (data.TryLandShip(travel, map, selectedMode))
                        Close();
                }

                curY += 78f;
            }

            Widgets.EndScrollView();
        }

        private void DrawModeSelector(Rect rect, OrbitalNode destination)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);

            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "Режим посадки");

            ShipLandingMode[] modes = new[]
            {
                ShipLandingMode.Precise,
                ShipLandingMode.Emergency,
                ShipLandingMode.OrbitalDrop,
                ShipLandingMode.UnpreparedSurface,
                ShipLandingMode.StationDocking
            };

            float y = inner.y + 28f;
            for (int i = 0; i < modes.Length; i++)
            {
                ShipLandingMode mode = modes[i];
                bool allowed = ShipLandingUtility.IsModeAllowedForDestination(mode, destination, out string reason);
                Rect buttonRect = new Rect(inner.x + (i % 2) * ((inner.width - 10f) / 2f + 10f), y + (i / 2) * 34f, (inner.width - 10f) / 2f, 28f);

                bool oldState = GUI.enabled;
                GUI.enabled = allowed;
                if (Widgets.ButtonText(buttonRect, (selectedMode == mode ? "● " : "○ ") + data.ResolveLandingModeLabel(mode)))
                    selectedMode = mode;
                GUI.enabled = oldState;

                if (!allowed)
                    TooltipHandler.TipRegion(buttonRect, reason);
            }

            Rect descRect = new Rect(inner.x, inner.y + 106f, inner.width, 24f);
            Rect consRect = new Rect(inner.x, inner.y + 128f, inner.width, 24f);
            Widgets.Label(descRect, "Описание: " + ShipLandingUtility.DescribeMode(selectedMode));
            Widgets.Label(consRect, "Последствия: " + ShipLandingUtility.DescribeModeConsequences(selectedMode));
        }

        private List<Map> BuildLandingMapList(OrbitalNode destination)
        {
            List<Map> maps = new List<Map>();

            if (destination != null)
            {
                Map targetMap = OrbitalNodeMapUtility.ResolveOrCreateMapForNode(destination);
                if (targetMap != null)
                    maps.Add(targetMap);
            }

            foreach (Map map in Find.Maps.Where(m => m != null && m.IsPlayerHome))
                if (!maps.Contains(map))
                    maps.Add(map);

            return maps;
        }
    }
}
