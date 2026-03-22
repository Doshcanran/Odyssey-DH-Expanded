using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    public class Building_ShipNavigationConsole : Building
    {
        private static readonly Texture2D LaunchIcon =
            ContentFinder<Texture2D>.Get("UI/LaunchShip", false) ?? TexButton.Play;


        private CompPowerTrader cachedPowerComp;

        public CompPowerTrader PowerComp
        {
            get
            {
                if (cachedPowerComp == null)
                    cachedPowerComp = GetComp<CompPowerTrader>();
                return cachedPowerComp;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
                yield return gizmo;

            yield return new Command_Action
            {
                defaultLabel = "IO_OrbitalMap".Translate(),
                defaultDesc = "IO_OrbitalMapDesc".Translate(),
                icon = LaunchIcon,
                action = delegate
                {
                    if (PowerComp != null && !PowerComp.PowerOn)
                    {
                        Messages.Message("IO_TerminalNoPower".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Thing ship = ShipResolver.FindBestAvailableShip(this);
                    if (ship == null)
                    {
                        Messages.Message("IO_NoShipNearTerminal".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Find.WindowStack.Add(new Window_OrbitalMap(ship));
                }
            };

            // Кнопка «На борт» если корабль в данный момент в полёте с картой вакуума
            WorldComponent_Interstellar wc = Find.World?.GetComponent<WorldComponent_Interstellar>();
            if (wc != null)
            {
                foreach (ShipTransitRecord tr in wc.activeTravels)
                {
                    if (tr == null || !VoidMapUtility.HasVoidMap(tr))
                        continue;

                    Map voidMap = VoidMapUtility.GetVoidMap(tr.voidMapTile);
                    if (voidMap == null)
                        continue;

                    ShipTransitRecord captured = tr;
                    yield return new Command_Action
                    {
                        defaultLabel = "IO_BoardShip".Translate(tr.shipLabel ?? "IO_ShipGeneric".Translate()),
                        defaultDesc = "IO_BoardShipDesc".Translate(),
                        action = delegate
                        {
                            Current.Game.CurrentMap = VoidMapUtility.GetVoidMap(captured.voidMapTile) ?? Current.Game.CurrentMap;
                        }
                    };
                }
            }

            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "IO_DebugShipBounds".Translate(),
                    defaultDesc = "IO_DebugShipBoundsDesc".Translate(),
                    action = delegate
                    {
                        Thing ship = ShipResolver.FindBestAvailableShip(this);
                        if (ship == null)
                        {
                            Messages.Message("IO_ShipNotFound".Translate(), MessageTypeDefOf.RejectInput, false);
                            return;
                        }

                        Messages.Message(
                            "IO_FoundShip".Translate(ship.LabelCap, ship.Position),
                            MessageTypeDefOf.TaskCompletion,
                            false);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "IO_DebugDiagnostics".Translate(),
                    defaultDesc = "IO_DebugDiagnosticsDesc".Translate(),
                    action = delegate
                    {
                        Find.WindowStack.Add(new Window_InterstellarDiagnostics());
                    }
                };
            }
        }
    }
}