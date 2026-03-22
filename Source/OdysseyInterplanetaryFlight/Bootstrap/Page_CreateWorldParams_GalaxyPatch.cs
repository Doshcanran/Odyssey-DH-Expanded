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

        [HarmonyPrefix]
        public static bool Prefix(Rect rect)
        {
            EnsureConfig();

            if (!InterstellarOdysseyMod.WorldGenGalaxyTabSelected)
                return true;

            DrawGalaxyOverlay(rect);
            return false;
        }

        [HarmonyPostfix]
        public static void Postfix(Rect rect)
        {
            EnsureConfig();

            if (InterstellarOdysseyMod.WorldGenGalaxyTabSelected)
                return;

            Rect safeRect = rect.ContractedBy(6f);

            const float buttonWidth = 220f;
            const float buttonHeight = 36f;
            const float footerReserved = 86f;
            const float sideMargin = 10f;
            const float buttonBottomGap = 6f;

            Rect toggleButtonRect = new Rect(
                safeRect.x + sideMargin,
                safeRect.yMax - footerReserved - buttonHeight - buttonBottomGap,
                buttonWidth,
                buttonHeight);

            if (Widgets.ButtonText(toggleButtonRect, "Галактики"))
                InterstellarOdysseyMod.WorldGenGalaxyTabSelected = true;
        }

        private static void EnsureConfig()
        {
            if (InterstellarOdysseyMod.PendingGalaxyConfig == null)
                InterstellarOdysseyMod.PendingGalaxyConfig = GalaxyConfigUtility.CreateDefaultConfiguration();

            GalaxyConfigUtility.EnsureConsistency(InterstellarOdysseyMod.PendingGalaxyConfig);
        }

        private static void DrawGalaxyOverlay(Rect rect)
        {
            Rect safeRect = rect.ContractedBy(6f);

            Widgets.DrawBoxSolid(safeRect, new Color(0.08f, 0.08f, 0.08f, 1f));
            Widgets.DrawMenuSection(safeRect);

            Rect inner = safeRect.ContractedBy(12f);

            const float headerHeight = 40f;
            Rect headerRect = new Rect(inner.x, inner.y, inner.width, headerHeight);
            Rect titleRect = new Rect(headerRect.x, headerRect.y + 5f, Mathf.Max(200f, headerRect.width - 520f), 28f);
            Rect factionsButtonRect = new Rect(headerRect.xMax - 730f, headerRect.y, 250f, 34f);
            Rect editorButtonRect = new Rect(headerRect.xMax - 470f, headerRect.y, 250f, 34f);
            Rect returnButtonRect = new Rect(headerRect.xMax - 210f, headerRect.y, 210f, 34f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(titleRect, "Настройка галактик, систем и архивов планет перед стартом.");

            if (Widgets.ButtonText(factionsButtonRect, "Фракции мира (ваниль)"))
                TryOpenVanillaFactionSettings();

            if (Widgets.ButtonText(editorButtonRect, "Открыть редактор галактик"))
                Find.WindowStack.Add(new Dialog_GalaxyWorldConfig(InterstellarOdysseyMod.PendingGalaxyConfig));

            if (Widgets.ButtonText(returnButtonRect, "Вернуться к миру"))
                InterstellarOdysseyMod.WorldGenGalaxyTabSelected = false;

            const float footerHeight = 28f;
            const float contentTopGap = 12f;

            Rect contentRect = new Rect(
                inner.x,
                headerRect.yMax + contentTopGap,
                inner.width,
                inner.height - headerHeight - contentTopGap - footerHeight - 8f);

            Rect footerRect = new Rect(inner.x, inner.yMax - footerHeight, inner.width, footerHeight);

            List<GalaxyDefinition> galaxies = InterstellarOdysseyMod.PendingGalaxyConfig.galaxies ?? new List<GalaxyDefinition>();
            const float rowHeight = 78f;
            const float rowGap = 8f;
            float calculatedViewHeight = galaxies.Count * (rowHeight + rowGap);
            float viewHeight = Mathf.Max(contentRect.height - 4f, calculatedViewHeight);
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
            Widgets.Label(footerRect, "Пока открыт экран галактик, ванильный интерфейс создания мира полностью заблокирован.");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void TryOpenVanillaFactionSettings()
        {
            try
            {
                Find.WindowStack.Add(new Dialog_AdvancedGameConfig());
            }
            catch (System.Exception ex)
            {
                Log.Error("[InterstellarOdyssey] Не удалось открыть ванильные настройки фракций: " + ex);
                Messages.Message("Не удалось открыть ванильное окно настройки фракций.", MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}
