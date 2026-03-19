using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
    public static class Page_CreateWorldParams_GalaxyPatch
    {
        private static Vector2 summaryScrollPos;

        [HarmonyPostfix]
        public static void Postfix(Rect rect)
        {
            if (InterstellarOdysseyMod.PendingGalaxyConfig == null)
                InterstellarOdysseyMod.PendingGalaxyConfig = GalaxyConfigUtility.CreateDefaultConfiguration();

            GalaxyConfigUtility.EnsureConsistency(InterstellarOdysseyMod.PendingGalaxyConfig);

            Rect safeRect = rect.ContractedBy(6f);

            float tabsWidth = 500f;
            float tabsHeight = 32f;
            float topOffset = Mathf.Clamp(safeRect.height * 0.06f, 30f, 56f);
            float bottomReserved = 86f;
            float tabsToWindowGap = 10f;

            // Вкладки рисуются отдельно НАД окном, а не врезаются в него.
            Rect tabsRect = new Rect(
                safeRect.x + 56f,
                safeRect.y + topOffset,
                tabsWidth,
                tabsHeight);

            Rect overlay = new Rect(
                safeRect.x + 10f,
                tabsRect.yMax + tabsToWindowGap,
                safeRect.width - 20f,
                safeRect.height - (tabsRect.yMax - safeRect.y) - tabsToWindowGap - bottomReserved);

            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("Мир", () => InterstellarOdysseyMod.WorldGenGalaxyTabSelected = false, !InterstellarOdysseyMod.WorldGenGalaxyTabSelected),
                new TabRecord("Галактики", () => InterstellarOdysseyMod.WorldGenGalaxyTabSelected = true, InterstellarOdysseyMod.WorldGenGalaxyTabSelected)
            };
            TabDrawer.DrawTabs(tabsRect, tabs);

            if (!InterstellarOdysseyMod.WorldGenGalaxyTabSelected)
                return;

            Widgets.DrawBoxSolid(overlay, new Color(0.10f, 0.10f, 0.10f, 0.985f));
            Widgets.DrawMenuSection(overlay);

            Rect inner = overlay.ContractedBy(12f);

            float headerHeight = 36f;
            Rect headerRect = new Rect(inner.x, inner.y, inner.width, headerHeight);
            Rect buttonRect = new Rect(headerRect.xMax - 260f, headerRect.y, 260f, 34f);
            Rect titleRect = new Rect(headerRect.x, headerRect.y + 3f, headerRect.width - 276f, 28f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(titleRect, "Настройка галактик, систем и архивов планет перед стартом.");

            if (Widgets.ButtonText(buttonRect, "Открыть редактор галактик"))
            {
                Find.WindowStack.Add(new Dialog_GalaxyWorldConfig(InterstellarOdysseyMod.PendingGalaxyConfig));
            }

            float footerHeight = 28f;
            float contentTopGap = 12f;
            Rect contentRect = new Rect(
                inner.x,
                headerRect.yMax + contentTopGap,
                inner.width,
                inner.height - headerHeight - contentTopGap - footerHeight - 8f);

            Rect footerRect = new Rect(inner.x, inner.yMax - footerHeight, inner.width, footerHeight);

            List<GalaxyDefinition> galaxies = InterstellarOdysseyMod.PendingGalaxyConfig.galaxies ?? new List<GalaxyDefinition>();
            float rowHeight = 78f;
            float rowGap = 8f;
            float viewHeight = Mathf.Max(contentRect.height - 4f, galaxies.Count * (rowHeight + rowGap));
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 18f, viewHeight);

            Widgets.BeginScrollView(contentRect, ref summaryScrollPos, viewRect);

            float y = 0f;
            for (int i = 0; i < galaxies.Count; i++)
            {
                GalaxyDefinition galaxy = galaxies[i];
                Rect row = new Rect(0f, y, viewRect.width, rowHeight);
                Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.04f));

                Rect rowInner = row.ContractedBy(10f);
                Rect summaryRect = new Rect(rowInner.x, rowInner.y + 4f, rowInner.width, 24f);
                Rect startRect = new Rect(rowInner.x, rowInner.y + 34f, rowInner.width, 22f);

                string summary = (galaxy.label ?? galaxy.id) +
                                 " | станции: " + (galaxy.hasStations ? galaxy.stationCount.ToString() : "выкл") +
                                 " | пояса: " + (galaxy.hasAsteroidBelts ? galaxy.beltCount.ToString() : "выкл") +
                                 " | планеты: " + (galaxy.hasPlanets ? galaxy.planetCount.ToString() : "выкл");

                Widgets.Label(summaryRect, summary);

                PlanetDefinition_IO startPlanet = galaxy.planets != null
                    ? galaxy.planets.Find(p => p != null && p.startPlanet)
                    : null;

                Widgets.Label(startRect, "Стартовая планета в галактике: " + (startPlanet != null ? startPlanet.label : "нет"));
                y += rowHeight + rowGap;
            }

            Widgets.EndScrollView();

            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            Widgets.Label(footerRect, "После создания мира конфигурация будет перенесена в WorldComponent и по ней будут сгенерированы узлы, архивы и вкладки галактик.");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
