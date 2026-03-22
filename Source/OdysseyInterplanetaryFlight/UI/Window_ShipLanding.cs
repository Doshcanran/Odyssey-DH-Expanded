using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Упрощённое окно посадки:
    /// - без выбора типа посадки
    /// - без выбора существующей карты
    /// - только выбор тайла на глобусе
    /// </summary>
    public class Window_ShipLanding : Window
    {
        private readonly ShipTransitRecord travel;
        private readonly WorldComponent_Interstellar data;

        private OrbitalNode Destination => travel != null
            ? data.GetNodeById(travel.destinationId)
            : null;

        public override Vector2 InitialSize => new Vector2(700f, 390f);

        public Window_ShipLanding(ShipTransitRecord travel)
        {
            this.travel = travel;
            data = Find.World.GetComponent<WorldComponent_Interstellar>();

            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            OrbitalNode dest = Destination;
            string targetLabel = data.ResolveNodeLabel(dest);

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "IO_ShipLandingTitle".Translate());

            Text.Font = GameFont.Small;
            Widgets.Label(
                new Rect(inRect.x, inRect.y + 38f, inRect.width, 22f),
                "IO_Target".Translate(targetLabel));

            Rect descriptionRect = new Rect(inRect.x, inRect.y + 64f, inRect.width, 52f);
            Widgets.Label(
                descriptionRect,
                "IO_SelectLandingPlaceDesc".Translate());

            Rect box = new Rect(inRect.x, inRect.y + 122f, inRect.width, 96f);
            Widgets.DrawMenuSection(box);

            Rect inner = box.ContractedBy(10f);
            Widgets.Label(
                new Rect(inner.x, inner.y, inner.width, 22f),
                "IO_StandardLandingMode".Translate());

            Widgets.Label(
                new Rect(inner.x, inner.y + 28f, inner.width, 42f),
                "IO_LandingModeDisabled".Translate());

            Rect buttonRect = new Rect(inRect.center.x - 180f, inRect.y + 238f, 360f, 38f);
            if (Widgets.ButtonText(buttonRect, "IO_SelectLandingPlace".Translate()))
            {
                BeginTileSelection();
            }

            Rect noteRect = new Rect(inRect.x + 10f, inRect.y + 288f, inRect.width - 20f, 40f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(noteRect, "IO_AfterTileSelected".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void BeginTileSelection()
        {
            ShipTransitRecord capturedTravel = travel;
            WorldComponent_Interstellar capturedData = data;

            if (capturedTravel == null)
            {
                Messages.Message("IO_NoTravelRecord".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            bool worldReady = capturedData.PrepareNewPlanetWorld(capturedTravel);
            if (!worldReady)
            {
                Messages.Message("IO_PreparePlanetFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            forcePause = false;
            Close();

            Find.World.renderer.wantedMode = WorldRenderMode.Planet;

            Find.WorldTargeter.BeginTargeting(
                action: (GlobalTargetInfo target) =>
                {
                    int tile = target.Tile;
                    if (!target.IsValid || tile < 0)
                        return false;

                    if (Find.WorldObjects.AnyWorldObjectAt(tile))
                    {
                        Messages.Message("IO_TileOccupied".Translate(), MessageTypeDefOf.RejectInput, false);
                        return false;
                    }

                    bool landed = capturedData.TryLandShipOnNewPlanet(
                        capturedTravel,
                        tile,
                        ShipLandingMode.Precise);

                    if (!landed)
                        Messages.Message("IO_LandingFailed".Translate(), MessageTypeDefOf.RejectInput, false);

                    return landed;
                },
                canTargetTiles: true);
        }
    }
}
