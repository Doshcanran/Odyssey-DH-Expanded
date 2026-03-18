using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class Building_ShipNavigationConsole : Building
        {
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
                    defaultLabel = "Орбитальная карта",
                    defaultDesc = "Открыть карту перелёта и выбрать пункт назначения.",
                    icon = BaseContent.BadTex,
                    action = delegate
                    {
                        if (PowerComp != null && !PowerComp.PowerOn)
                        {
                            Messages.Message("Терминал не запитан.", MessageTypeDefOf.RejectInput, false);
                            return;
                        }

                        Thing ship = ShipResolver.FindBestAvailableShip(this);
                        if (ship == null)
                        {
                            Messages.Message("Не найден гравикорабль рядом с терминалом.", MessageTypeDefOf.RejectInput, false);
                            return;
                        }

                        Find.WindowStack.Add(new Window_OrbitalMap(ship));
                    }
                };
            }
        }

        public class PlaceWorker_ShipNavigationConsole : PlaceWorker
        {
            public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
            {
                if (!ShipFloorUtility.IsShipFloorCell(map, loc))
                    return new AcceptanceReport("Терминал можно ставить только на палубу/надстройку корабля.");

                return AcceptanceReport.WasAccepted;
            }
        }

        public static class ShipResolver
        {
            public static Thing FindBestAvailableShip(Building console)
            {
                if (console == null || console.Map == null)
                    return null;

                return ShipCaptureUtility.FindShipAnchorForConsole(console);
            }
        }
}
