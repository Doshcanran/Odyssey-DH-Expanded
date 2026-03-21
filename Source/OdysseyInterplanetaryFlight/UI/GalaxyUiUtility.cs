using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace InterstellarOdyssey
{
    public static class GalaxyUiUtility
    {
        public static float DrawGalaxyTabs(Rect rect, WorldComponent_Interstellar data)
        {
            if (data == null)
                return rect.yMax;

            List<GalaxyDefinition> galaxies = new List<GalaxyDefinition>();
            foreach (GalaxyDefinition galaxy in data.GetGalaxies())
            {
                if (galaxy != null)
                    galaxies.Add(galaxy);
            }

            if (galaxies.Count == 0)
                return rect.yMax;

            const float buttonWidth = 180f;
            const float buttonHeight = 32f;
            const float gap = 8f;

            float curX = rect.x;
            float curY = rect.y;

            foreach (GalaxyDefinition galaxy in galaxies)
            {
                if (curX + buttonWidth > rect.xMax && curX > rect.x)
                {
                    curX = rect.x;
                    curY += buttonHeight + gap;
                }

                Rect buttonRect = new Rect(curX, curY, buttonWidth, buttonHeight);
                bool isSelected = data.selectedGalaxyId == galaxy.id;

                Color oldColor = GUI.color;
                if (isSelected)
                    GUI.color = new Color(0.72f, 0.86f, 1f);

                if (Widgets.ButtonText(buttonRect, galaxy.label ?? galaxy.id))
                    data.SelectGalaxy(galaxy.id);

                GUI.color = oldColor;
                curX += buttonWidth + gap;
            }

            return curY + buttonHeight;
        }

        public static IEnumerable<OrbitalNode> VisibleNodes(WorldComponent_Interstellar data)
        {
            return data.GetNodesForGalaxy(data.selectedGalaxyId);
        }
    }
}
