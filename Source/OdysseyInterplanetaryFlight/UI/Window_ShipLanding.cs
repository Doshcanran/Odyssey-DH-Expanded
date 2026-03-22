using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Окно посадки корабля после межпланетного перелёта.
    ///
    /// Для ПЛАНЕТ (любых): сначала предлагает выбрать тайл на глобусе
    ///   (как в ванили DLC Odyssey), затем список существующих карт.
    /// Для СТАНЦИЙ / АСТЕРОИДОВ: сразу список существующих карт.
    /// </summary>
    public class Window_ShipLanding : Window
    {
        private readonly ShipTransitRecord travel;
        private readonly WorldComponent_Interstellar data;
        private Vector2 scrollPos;
        private ShipLandingMode selectedMode;

        private OrbitalNode Destination => travel != null
            ? data.GetNodeById(travel.destinationId)
            : null;

        private bool IsPlanetDestination => Destination?.type == OrbitalNodeType.Planet;

        public override Vector2 InitialSize => new Vector2(860f, 720f);

        public Window_ShipLanding(ShipTransitRecord travel)
        {
            this.travel   = travel;
            data          = Find.World.GetComponent<WorldComponent_Interstellar>();
            selectedMode  = travel?.preferredLandingMode ?? ShipLandingMode.Precise;
            forcePause    = true;
            doCloseX      = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            OrbitalNode dest = Destination;

            // Заголовок
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Посадка корабля");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 34f, inRect.width - 170f, 22f),
                "Цель: " + data.ResolveNodeLabel(dest));

            // Кнопка «На борт карты вакуума»
            if (travel != null && VoidMapUtility.HasVoidMap(travel))
            {
                if (Widgets.ButtonText(new Rect(inRect.xMax - 160f, inRect.y + 30f, 150f, 26f), "На борт корабля"))
                {
                    Map vm = VoidMapUtility.GetVoidMap(travel.voidMapTile);
                    if (vm != null) { Current.Game.CurrentMap = vm; Close(); }
                }
            }

            // Режим посадки
            Rect modeRect = new Rect(inRect.x, inRect.y + 62f, inRect.width, 170f);
            DrawModeSelector(modeRect, dest);

            float y = modeRect.yMax + 10f;

            if (IsPlanetDestination)
            {
                // ── Блок выбора нового тайла ────────────────────────────────
                Rect chooseRect = new Rect(inRect.x, y, inRect.width, 64f);
                Widgets.DrawMenuSection(chooseRect);
                Rect ci = chooseRect.ContractedBy(10f);

                string planetName = data.ResolveNodeLabel(dest);
                Widgets.Label(new Rect(ci.x, ci.y, ci.width - 240f, 20f),
                    "Выберите место посадки на планете «" + planetName + "»:");
                Widgets.Label(new Rect(ci.x, ci.y + 22f, ci.width - 240f, 18f),
                    "Кликните тайл на глобусе — там будет создано новое поселение.");

                if (Widgets.ButtonText(new Rect(ci.xMax - 230f, ci.y + 4f, 230f, 34f),
                    "🌍  Выбрать тайл на глобусе"))
                {
                    BeginTileSelection(dest);
                    return; // окно закрывается внутри BeginTileSelection
                }

                y = chooseRect.yMax + 8f;

                // ── Разделитель «или вернуться в существующее» ───────────────
                List<Map> existingMaps = GetExistingPlayerMaps();
                if (existingMaps.Count > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);
                    Widgets.DrawLineHorizontal(inRect.x, y + 10f, inRect.width);
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(inRect.x, y + 2f, inRect.width, 22f),
                        "— или вернуться в существующее поселение —");
                    y += 26f;
                    DrawMapList(inRect, ref y, existingMaps);
                }
            }
            else
            {
                // ── Станции / астероиды — прямой список карт ─────────────────
                List<Map> maps = BuildNonPlanetMapList(dest);
                DrawMapList(inRect, ref y, maps);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Выбор тайла на глобусе
        // ─────────────────────────────────────────────────────────────────────

        private void BeginTileSelection(OrbitalNode destination)
        {
            ShipTransitRecord capturedTravel = travel;
            ShipLandingMode   capturedMode   = selectedMode;
            WorldComponent_Interstellar capturedData = data;
            OrbitalNode       capturedDest   = destination;

            // ── Шаг 1: перегенерировать мировой шар ДО открытия глобуса ──────
            // Игрок будет выбирать тайл уже на НОВОЙ планете
            bool worldReady = capturedData.PrepareNewPlanetWorld(capturedTravel);
            if (!worldReady)
            {
                Messages.Message("Не удалось подготовить новую планету.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            // ── Шаг 2: закрываем окно, открываем глобус новой планеты ─────────
            forcePause = false;
            Close();

            Find.World.renderer.wantedMode = WorldRenderMode.Planet;

            // ── Шаг 3: игрок выбирает тайл на новом мировом шаре ─────────────
            Find.WorldTargeter.BeginTargeting(
                action: (GlobalTargetInfo target) =>
                {
                    int tile = target.Tile;
                    if (!target.IsValid || tile < 0) return false;
                    if (Find.WorldObjects.AnyWorldObjectAt(tile))
                    {
                        Messages.Message("Этот тайл занят. Выберите другой.",
                            MessageTypeDefOf.RejectInput, false);
                        return false;
                    }

                    // ── Шаг 4: генерируем карту на тайле и садимся ────────────
                    // Мир уже перегенерирован — tile валиден на новой планете
                    bool landed = capturedData.TryLandShipOnNewPlanet(
                        capturedTravel, tile, capturedMode);

                    if (!landed)
                        Messages.Message("Посадка не удалась.", MessageTypeDefOf.RejectInput, false);

                    return landed;
                },
                canTargetTiles: true);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Рисуем список карт
        // ─────────────────────────────────────────────────────────────────────

        private void DrawMapList(Rect inRect, ref float y, List<Map> maps)
        {
            if (maps.Count == 0) return;

            float rowH    = 72f;
            Rect outRect  = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f,
                Mathf.Max(rowH, maps.Count * rowH));

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float curY = 0f;

            foreach (Map map in maps)
            {
                Rect row = new Rect(0f, curY, viewRect.width, rowH - 4f);
                Widgets.DrawMenuSection(row);

                string lbl = map.Parent?.LabelCap ?? "Карта";
                Widgets.Label(new Rect(row.x + 10f, row.y + 8f,  row.width - 180f, 22f), lbl);
                Widgets.Label(new Rect(row.x + 10f, row.y + 30f, row.width - 180f, 20f),
                    "Размер: " + map.Size.x + "×" + map.Size.z
                    + "  |  Режим: " + data.ResolveLandingModeLabel(selectedMode));

                if (Widgets.ButtonText(new Rect(row.width - 148f, row.y + 18f, 138f, 30f), "Посадить здесь"))
                {
                    if (data.TryLandShip(travel, map, selectedMode))
                        Close();
                }

                curY += rowH;
            }

            Widgets.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Списки карт
        // ─────────────────────────────────────────────────────────────────────

        private List<Map> GetExistingPlayerMaps()
        {
            List<Map> result = Find.Maps
                .Where(m => m != null && m.IsPlayerHome)
                .ToList();

            // Исключаем карту вакуума
            if (travel?.voidMapTile >= 0)
                result.Remove(VoidMapUtility.GetVoidMap(travel.voidMapTile));

            return result;
        }

        private List<Map> BuildNonPlanetMapList(OrbitalNode destination)
        {
            List<Map> maps = new List<Map>();

            if (destination != null)
            {
                Map targetMap = OrbitalNodeMapUtility.ResolveOrCreateMapForNode(destination);
                if (targetMap != null) maps.Add(targetMap);
            }

            foreach (Map m in Find.Maps.Where(m => m != null && m.IsPlayerHome))
                if (!maps.Contains(m)) maps.Add(m);

            if (travel?.voidMapTile >= 0)
                maps.Remove(VoidMapUtility.GetVoidMap(travel.voidMapTile));

            return maps;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Режим посадки
        // ─────────────────────────────────────────────────────────────────────

        private void DrawModeSelector(Rect rect, OrbitalNode destination)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "Режим посадки");

            ShipLandingMode[] modes =
            {
                ShipLandingMode.Precise,
                ShipLandingMode.Emergency,
                ShipLandingMode.OrbitalDrop,
                ShipLandingMode.UnpreparedSurface,
                ShipLandingMode.StationDocking
            };

            float colW = (inner.width - 10f) / 2f;
            float y    = inner.y + 28f;

            for (int i = 0; i < modes.Length; i++)
            {
                ShipLandingMode mode    = modes[i];
                bool allowed = ShipLandingUtility.IsModeAllowedForDestination(
                    mode, destination, out string reason);
                Rect btn = new Rect(inner.x + (i % 2) * (colW + 10f),
                    y + (i / 2) * 34f, colW, 28f);

                bool was = GUI.enabled;
                GUI.enabled = allowed;
                if (Widgets.ButtonText(btn,
                    (selectedMode == mode ? "● " : "○ ") + data.ResolveLandingModeLabel(mode)))
                    selectedMode = mode;
                GUI.enabled = was;

                if (!allowed) TooltipHandler.TipRegion(btn, reason);
            }

            Widgets.Label(new Rect(inner.x, inner.y + 106f, inner.width, 22f),
                "Описание: " + ShipLandingUtility.DescribeMode(selectedMode));
            Widgets.Label(new Rect(inner.x, inner.y + 128f, inner.width, 22f),
                "Последствия: " + ShipLandingUtility.DescribeModeConsequences(selectedMode));
        }
    }
}
