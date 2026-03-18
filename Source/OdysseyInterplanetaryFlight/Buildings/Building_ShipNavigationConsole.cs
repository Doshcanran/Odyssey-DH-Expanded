using System.Collections.Generic;
using RimWorld;
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
                        Messages.Message("Не найден корабль рядом с терминалом.", MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Find.WindowStack.Add(new Window_OrbitalMap(ship));
                }
            };

            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Границы корабля",
                    defaultDesc = "Проверить, какой корабль видит навигационный терминал.",
                    icon = BaseContent.BadTex,
                    action = delegate
                    {
                        Thing ship = ShipResolver.FindBestAvailableShip(this);
                        if (ship == null)
                        {
                            Messages.Message("Корабль не найден.", MessageTypeDefOf.RejectInput, false);
                            return;
                        }

                        Messages.Message(
                            "Найден корабль: " + ship.LabelCap + " @ " + ship.Position,
                            MessageTypeDefOf.TaskCompletion,
                            false);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Диагностика",
                    defaultDesc = "Открыть окно диагностики перелёта.",
                    icon = BaseContent.BadTex,
                    action = delegate
                    {
                        Find.WindowStack.Add(new Window_InterstellarDiagnostics());
                    }
                };
            }
        }
    }
}