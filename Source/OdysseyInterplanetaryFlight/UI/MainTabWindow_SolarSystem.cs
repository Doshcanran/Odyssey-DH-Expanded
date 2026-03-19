using RimWorld;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public class MainTabWindow_SolarSystem : MainTabWindow
    {
        public override Vector2 RequestedTabSize => new Vector2(UI.screenWidth - 40f, UI.screenHeight - 120f);

        public override void PreOpen()
        {
            base.PreOpen();
            Find.World.GetComponent<WorldComponent_Interstellar>().GenerateIfNeeded();
        }

        public override void DoWindowContents(Rect inRect)
        {
            WorldComponent_Interstellar data = Find.World.GetComponent<WorldComponent_Interstellar>();
            data.GenerateIfNeeded();

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, Mathf.Min(inRect.width - 20f, 420f), 32f), "Солнечная система");
            Text.Font = GameFont.Small;

            Rect tabsRect = new Rect(inRect.x, inRect.y + 40f, inRect.width - 20f, 32f);
            GalaxyUiUtility.DrawGalaxyTabs(tabsRect, data);

            Rect infoRect = new Rect(inRect.x, tabsRect.yMax + 8f, inRect.width, 24f);
            Widgets.Label(infoRect, "Активная галактика: " + (data.GetGalaxyById(data.selectedGalaxyId)?.label ?? data.selectedGalaxyId));

            Rect body = new Rect(inRect.x, infoRect.yMax + 8f, inRect.width, inRect.height - (infoRect.yMax - inRect.y) - 8f);
            Window_TransitMonitor.DrawSolarSystemUI(body, data, null, true, data.selectedGalaxyId);
        }
    }

    public class MainButtonWorker_InterstellarSystem : MainButtonWorker
    {
        public override void Activate()
        {
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("IO_SolarSystem");
            if (def == null)
            {
                Messages.Message("Не найден MainButtonDef IO_SolarSystem. Проверь Defs/MainButtons/IO_MainButtons.xml", MessageTypeDefOf.RejectInput, false);
                Log.Error("InterstellarOdyssey: MainButtonDef IO_SolarSystem not found.");
                return;
            }

            Find.MainTabsRoot.SetCurrentTab(def, true);
        }
    }
}
