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

            List<TabRecord> tabs = new List<TabRecord>();
            foreach (GalaxyDefinition galaxy in data.GetGalaxies())
            {
                if (galaxy == null)
                    continue;

                string captured = galaxy.id;
                tabs.Add(new TabRecord(
                    galaxy.label ?? captured,
                    delegate { data.SelectGalaxy(captured); },
                    data.selectedGalaxyId == captured));
            }

            if (tabs.Count == 0)
                return rect.yMax;

            Rect tabsRect = rect;
            tabsRect.width = Mathf.Min(rect.width, tabs.Count * 220f);
            TabDrawer.DrawTabs(tabsRect, tabs);
            return tabsRect.yMax;
        }

        public static IEnumerable<OrbitalNode> VisibleNodes(WorldComponent_Interstellar data)
        {
            return data.GetNodesForGalaxy(data.selectedGalaxyId);
        }
    }
}
