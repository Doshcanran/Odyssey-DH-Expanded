using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public class Dialog_GalaxyWorldConfig : Window
    {
        private Vector2 scrollPos;
        private readonly GalaxyWorldConfiguration config;
        private readonly Dictionary<string, string> intBuffers = new Dictionary<string, string>();
        private readonly Dictionary<string, string> textBuffers = new Dictionary<string, string>();

        public override Vector2 InitialSize => new Vector2(1180f, 820f);

        public Dialog_GalaxyWorldConfig(GalaxyWorldConfiguration config)
        {
            this.config = config ?? GalaxyConfigUtility.CreateDefaultConfiguration();
            GalaxyConfigUtility.EnsureConsistency(this.config);
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseButton = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            optionalTitle = "Настройки галактик";
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect countRect = new Rect(inRect.x, inRect.y, inRect.width, 42f);
            DrawCountSelector(countRect);

            Rect outRect = new Rect(inRect.x, countRect.yMax + 10f, inRect.width, inRect.height - 96f);
            float estimatedHeight = Mathf.Max(outRect.height, config.galaxies.Count * 436f + 80f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, estimatedHeight);

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float curY = 0f;

            for (int i = 0; i < config.galaxies.Count; i++)
            {
                GalaxyDefinition galaxy = config.galaxies[i];
                Rect block = new Rect(0f, curY, viewRect.width, 420f);
                DrawGalaxyBlock(block, galaxy, i);
                curY += block.height + 12f;
            }

            Widgets.EndScrollView();

            Rect bottomRect = new Rect(inRect.x, inRect.yMax - 34f, inRect.width, 30f);
            if (Widgets.ButtonText(new Rect(bottomRect.xMax - 220f, bottomRect.y, 220f, 30f), "Сохранить настройки"))
            {
                GalaxyConfigUtility.EnsureConsistency(config);
                InterstellarOdysseyMod.PendingGalaxyConfig = config;
                Close();
            }
        }

        private void DrawCountSelector(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);
            Widgets.Label(new Rect(inner.x, inner.y + 6f, 180f, 24f), "Количество галактик:");

            float x = inner.x + 190f;
            DrawCountButton(new Rect(x, inner.y + 2f, 60f, 28f), 1, ref config.selectedGalaxyCount);
            x += 66f;
            DrawCountButton(new Rect(x, inner.y + 2f, 60f, 28f), 2, ref config.selectedGalaxyCount);
            x += 66f;
            DrawCountButton(new Rect(x, inner.y + 2f, 60f, 28f), 5, ref config.selectedGalaxyCount);
            x += 70f;

            string key = "galaxy_count";
            string buffer = GetBuffer(key, config.selectedGalaxyCount.ToString());
            Widgets.Label(new Rect(x, inner.y + 6f, 70f, 24f), "Своё:");
            buffer = Widgets.TextField(new Rect(x + 55f, inner.y + 2f, 80f, 28f), buffer);
            intBuffers[key] = buffer;
            if (Widgets.ButtonText(new Rect(x + 142f, inner.y + 2f, 72f, 28f), "Принять"))
            {
                if (int.TryParse(buffer, out int custom))
                    config.selectedGalaxyCount = Mathf.Clamp(custom, 1, 20);
            }

            GalaxyConfigUtility.EnsureConsistency(config);
        }

        private void DrawCountButton(Rect rect, int value, ref int current)
        {
            if (Widgets.ButtonText(rect, (current == value ? "● " : "○ ") + value))
                current = value;
        }

        private void DrawGalaxyBlock(Rect rect, GalaxyDefinition galaxy, int index)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            string labelKey = "galaxy_label_" + index;
            string labelBuffer = GetTextBuffer(labelKey, galaxy.label ?? ("Галактика " + (index + 1)));
            Widgets.Label(new Rect(inner.x, inner.y, 120f, 24f), "Название:");
            labelBuffer = Widgets.TextField(new Rect(inner.x + 90f, inner.y, 260f, 24f), labelBuffer);
            textBuffers[labelKey] = labelBuffer;
            galaxy.label = labelBuffer;

            float y = inner.y + 36f;
            Widgets.CheckboxLabeled(new Rect(inner.x, y, 220f, 24f), "Орбитальные станции", ref galaxy.hasStations);
            DrawCountOption(inner.x + 250f, y, "stationCount_" + index, ref galaxy.stationCount);
            y += 32f;

            Widgets.CheckboxLabeled(new Rect(inner.x, y, 220f, 24f), "Пояса астероидов", ref galaxy.hasAsteroidBelts);
            DrawCountOption(inner.x + 250f, y, "beltCount_" + index, ref galaxy.beltCount);
            y += 32f;

            Widgets.CheckboxLabeled(new Rect(inner.x, y, 220f, 24f), "Планеты", ref galaxy.hasPlanets);
            DrawCountOption(inner.x + 250f, y, "planetCount_" + index, ref galaxy.planetCount);
            y += 42f;

            GalaxyConfigUtility.EnsureConsistency(config);

            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "Планеты галактики");
            y += 28f;

            float rowHeight = 108f;
            int maxPlanetsToDraw = Mathf.Min(galaxy.planets.Count, 6);
            for (int p = 0; p < maxPlanetsToDraw; p++)
            {
                PlanetDefinition_IO planet = galaxy.planets[p];
                Rect row = new Rect(inner.x, y, inner.width, rowHeight);
                DrawPlanetRow(row, planet, galaxy, index, p);
                y += rowHeight + 8f;
            }
        }

        private void DrawCountOption(float x, float y, string key, ref int value)
        {
            Rect r1 = new Rect(x, y, 54f, 24f);
            Rect r2 = new Rect(x + 58f, y, 54f, 24f);
            Rect r5 = new Rect(x + 116f, y, 54f, 24f);
            if (Widgets.ButtonText(r1, value == 1 ? "●1" : "○1")) value = 1;
            if (Widgets.ButtonText(r2, value == 2 ? "●2" : "○2")) value = 2;
            if (Widgets.ButtonText(r5, value == 5 ? "●5" : "○5")) value = 5;

            string buffer = GetBuffer(key, value.ToString());
            buffer = Widgets.TextField(new Rect(x + 178f, y, 64f, 24f), buffer);
            intBuffers[key] = buffer;
            if (Widgets.ButtonText(new Rect(x + 246f, y, 70f, 24f), "Своё"))
            {
                if (int.TryParse(buffer, out int custom))
                    value = Mathf.Clamp(custom, 1, 20);
            }
        }

        private void DrawPlanetRow(Rect rect, PlanetDefinition_IO planet, GalaxyDefinition galaxy, int galaxyIndex, int planetIndex)
        {
            Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.04f));
            Rect inner = rect.ContractedBy(8f);

            string key = "planet_label_" + galaxyIndex + "_" + planetIndex;
            string labelBuffer = GetTextBuffer(key, planet.label ?? ("Планета " + (planetIndex + 1)));
            Widgets.Label(new Rect(inner.x, inner.y, 52f, 24f), "Имя:");
            labelBuffer = Widgets.TextField(new Rect(inner.x + 46f, inner.y, 200f, 24f), labelBuffer);
            textBuffers[key] = labelBuffer;
            planet.label = labelBuffer;

            bool start = planet.startPlanet;
            Widgets.CheckboxLabeled(new Rect(inner.x + 260f, inner.y, 120f, 24f), "Стартовая", ref start);
            if (start != planet.startPlanet && start)
            {
                foreach (GalaxyDefinition g in config.galaxies)
                    foreach (PlanetDefinition_IO p in g.planets)
                        p.startPlanet = false;
            }
            planet.startPlanet = start;

            bool defaults = planet.useVanillaDefaults;
            Widgets.CheckboxLabeled(new Rect(inner.x + 400f, inner.y, 190f, 24f), "Ванильные дефолты", ref defaults);
            planet.useVanillaDefaults = defaults;

            float y = inner.y + 32f;
            DrawSlider(new Rect(inner.x, y, 320f, 22f), "Температура", ref planet.overallTemperature, 0f, 2f);
            DrawSlider(new Rect(inner.x + 330f, y, 320f, 22f), "Осадки", ref planet.overallRainfall, 0f, 2f);
            DrawSlider(new Rect(inner.x + 660f, y, 320f, 22f), "Население", ref planet.overallPopulation, 0f, 2f);
            y += 32f;
            DrawSlider(new Rect(inner.x, y, 320f, 22f), "Покрытие", ref planet.coverage, 0.05f, 1f);
            DrawSlider(new Rect(inner.x + 330f, y, 320f, 22f), "Загрязнение", ref planet.pollution, 0f, 1f);
            DrawSeedField(new Rect(inner.x + 660f, y, 180f, 24f), planet, galaxyIndex, planetIndex);
        }

        private void DrawSlider(Rect rect, string label, ref float value, float min, float max)
        {
            Widgets.Label(new Rect(rect.x, rect.y, 112f, rect.height), label + ": " + value.ToString("0.00"));
            value = Widgets.HorizontalSlider(new Rect(rect.x + 114f, rect.y, rect.width - 116f, rect.height), value, min, max, true);
        }

        private void DrawSeedField(Rect rect, PlanetDefinition_IO planet, int galaxyIndex, int planetIndex)
        {
            string key = "seed_" + galaxyIndex + "_" + planetIndex;
            string buffer = GetBuffer(key, planet.seedOffset.ToString());
            Widgets.Label(new Rect(rect.x, rect.y, 50f, 22f), "Seed:");
            buffer = Widgets.TextField(new Rect(rect.x + 44f, rect.y, 96f, 22f), buffer);
            intBuffers[key] = buffer;
            if (int.TryParse(buffer, out int seedOffset))
                planet.seedOffset = seedOffset;
        }

        private string GetBuffer(string key, string fallback)
        {
            if (!intBuffers.TryGetValue(key, out string value))
            {
                value = fallback;
                intBuffers[key] = value;
            }

            return value;
        }

        private string GetTextBuffer(string key, string fallback)
        {
            if (!textBuffers.TryGetValue(key, out string value))
            {
                value = fallback;
                textBuffers[key] = value;
            }

            return value;
        }
    }
}
