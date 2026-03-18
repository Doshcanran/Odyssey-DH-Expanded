using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public class Window_InterstellarDiagnostics : Window
    {
        private readonly WorldComponent_Interstellar data;
        private Vector2 scrollPos;
        private string categoryFilter = "Все";

        public override Vector2 InitialSize => new Vector2(980f, 680f);

        public Window_InterstellarDiagnostics()
        {
            data = Find.World.GetComponent<WorldComponent_Interstellar>();
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            optionalTitle = "Журнал диагностики Interstellar Odyssey";
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (data == null)
            {
                Widgets.Label(inRect, "Диагностический компонент мира не найден.");
                return;
            }

            Rect topBar = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Widgets.Label(new Rect(topBar.x, topBar.y, 260f, topBar.height), "Категория:");
            if (Widgets.ButtonText(new Rect(topBar.x + 92f, topBar.y, 130f, topBar.height), categoryFilter))
                Find.WindowStack.Add(BuildCategoryMenu());

            if (Widgets.ButtonText(new Rect(topBar.xMax - 240f, topBar.y, 110f, topBar.height), "Очистить"))
                data.ClearDiagnostics();

            if (Widgets.ButtonText(new Rect(topBar.xMax - 120f, topBar.y, 110f, topBar.height), "Скопировать"))
                GUIUtility.systemCopyBuffer = data.BuildDiagnosticsDump();

            Rect outRect = new Rect(inRect.x, topBar.yMax + 8f, inRect.width, inRect.height - topBar.height - 8f);
            List<InterstellarDiagnosticEntry> entries = data.GetDiagnostics(categoryFilter).ToList();
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height - 4f, entries.Count * 86f));

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float curY = 0f;

            if (entries.Count == 0)
            {
                Widgets.Label(new Rect(0f, 0f, viewRect.width, 30f), "Записей нет.");
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    InterstellarDiagnosticEntry entry = entries[i];
                    Rect row = new Rect(0f, curY, viewRect.width, 80f);
                    Widgets.DrawMenuSection(row);

                    string severity = entry.severity == InterstellarDiagnosticSeverity.Error
                        ? "Ошибка"
                        : (entry.severity == InterstellarDiagnosticSeverity.Warning ? "Предупреждение" : "Инфо");

                    Widgets.Label(new Rect(row.x + 8f, row.y + 6f, row.width - 16f, 22f), "[" + severity + "] " + (entry.title ?? "Запись"));
                    Widgets.Label(new Rect(row.x + 8f, row.y + 28f, row.width - 16f, 22f), (entry.category ?? "Общее") + " | Tick: " + entry.tick);
                    Widgets.Label(new Rect(row.x + 8f, row.y + 48f, row.width - 16f, 24f), entry.message ?? string.Empty);

                    if (!string.IsNullOrEmpty(entry.details))
                        TooltipHandler.TipRegion(row, entry.details);

                    curY += 86f;
                }
            }

            Widgets.EndScrollView();
        }

        private FloatMenu BuildCategoryMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Все", delegate { categoryFilter = "Все"; })
            };

            foreach (string category in data.diagnostics.Where(d => d != null && !string.IsNullOrEmpty(d.category)).Select(d => d.category).Distinct().OrderBy(c => c))
                options.Add(new FloatMenuOption(category, delegate { categoryFilter = category; }));

            return new FloatMenu(options);
        }
    }
}
