using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public static class ShipTransitEventUtility
        {
            private const int MinEventIntervalTicks = 12000;
            private const int MaxEventIntervalTicks = 26000;

            public static void ScheduleNextEvent(ShipTransitRecord record, int currentTick)
            {
                if (record == null)
                    return;

                record.nextEventTick = currentTick + Rand.RangeInclusive(MinEventIntervalTicks, MaxEventIntervalTicks);
            }

            public static bool TryProcessEvent(WorldComponent_Interstellar data, ShipTransitRecord record)
            {
                if (data == null || record == null || record.stage != InterstellarTransitStage.InTransit || Find.TickManager == null)
                    return false;

                int currentTick = Find.TickManager.TicksGame;
                if (record.nextEventTick <= 0)
                    ScheduleNextEvent(record, currentTick);

                if (currentTick < record.nextEventTick)
                    return false;

                ScheduleNextEvent(record, currentTick);

                if (!Rand.Chance(0.55f))
                    return false;

                ShipTransitEvent transitEvent = GenerateEvent(data, record, currentTick);
                if (transitEvent == null)
                    return false;

                record.eventLog.Add(transitEvent);
                if (record.eventLog.Count > 24)
                    record.eventLog.RemoveAt(0);

                Messages.Message(transitEvent.title + ": " + transitEvent.description, MessageTypeDefOf.NeutralEvent, false);
                return true;
            }

            private static ShipTransitEvent GenerateEvent(WorldComponent_Interstellar data, ShipTransitRecord record, int currentTick)
            {
                float roll = Rand.Value;
                if (roll < 0.18f)
                    return ApplyEngineBreakdown(record, currentTick);

                if (roll < 0.36f)
                    return ApplyEnergyLeak(record, currentTick);

                if (roll < 0.54f)
                    return ApplySolarStorm(record, currentTick);

                if (roll < 0.72f)
                    return ApplyDrift(record, currentTick);

                if (roll < 0.86f)
                    return ApplyPirateSignal(record, currentTick);

                return ApplyDebrisDiscovery(record, currentTick);
            }

            private static ShipTransitEvent ApplyEngineBreakdown(ShipTransitRecord record, int currentTick)
            {
                int delay = Mathf.RoundToInt(0.22f * GenDate.TicksPerDay);
                record.arrivalTick += delay;
                record.travelDisruption += 0.22f;

                return new ShipTransitEvent
                {
                    type = TransitEventType.EngineBreakdown,
                    tick = currentTick,
                    title = "Поломка двигателя",
                    description = "Тяга временно упала. Прибытие задержано примерно на " + (delay / 2500f).ToString("0.0") + " д.",
                    severity = 0.7f
                };
            }

            private static ShipTransitEvent ApplyEnergyLeak(ShipTransitRecord record, int currentTick)
            {
                int delay = Mathf.RoundToInt(0.10f * GenDate.TicksPerDay);
                record.arrivalTick += delay;
                record.travelDisruption += 0.10f;

                return new ShipTransitEvent
                {
                    type = TransitEventType.EnergyLeak,
                    tick = currentTick,
                    title = "Утечка энергии",
                    description = "Экипаж перераспределил питание по системам. Полёт слегка замедлился.",
                    severity = 0.35f
                };
            }

            private static ShipTransitEvent ApplySolarStorm(ShipTransitRecord record, int currentTick)
            {
                int delay = Mathf.RoundToInt(0.16f * GenDate.TicksPerDay);
                record.arrivalTick += delay;
                record.travelDisruption += 0.16f;

                return new ShipTransitEvent
                {
                    type = TransitEventType.SolarStorm,
                    tick = currentTick,
                    title = "Солнечная буря",
                    description = "Корабль вошёл в зону вспышки и был вынужден снизить ход.",
                    severity = 0.55f
                };
            }

            private static ShipTransitEvent ApplyDrift(ShipTransitRecord record, int currentTick)
            {
                int delay = Mathf.RoundToInt(0.28f * GenDate.TicksPerDay);
                record.arrivalTick += delay;
                record.travelDisruption += 0.28f;
                record.preferredLandingMode = ShipLandingMode.Emergency;

                return new ShipTransitEvent
                {
                    type = TransitEventType.Drift,
                    tick = currentTick,
                    title = "Дрейф",
                    description = "Корабль отклонился от траектории. Рекомендуется аварийная посадка.",
                    severity = 0.8f
                };
            }

            private static ShipTransitEvent ApplyPirateSignal(ShipTransitRecord record, int currentTick)
            {
                record.preferredLandingMode = record.preferredLandingMode == ShipLandingMode.Precise
                    ? ShipLandingMode.OrbitalDrop
                    : record.preferredLandingMode;

                return new ShipTransitEvent
                {
                    type = TransitEventType.PirateSignal,
                    tick = currentTick,
                    title = "Пиратский сигнал",
                    description = "Зафиксирован подозрительный источник на дальней дистанции. Возможна горячая посадка.",
                    severity = 0.45f
                };
            }

            private static ShipTransitEvent ApplyDebrisDiscovery(ShipTransitRecord record, int currentTick)
            {
                int steel = Rand.RangeInclusive(35, 90);
                int components = Rand.Chance(0.45f) ? Rand.RangeInclusive(1, 3) : 0;
                record.salvageSteel += steel;
                record.salvageComponents += components;

                return new ShipTransitEvent
                {
                    type = TransitEventType.DebrisDiscovery,
                    tick = currentTick,
                    title = "Находка обломков",
                    description = "Подобраны ценные материалы: стали +" + steel + (components > 0 ? ", компонентов +" + components : string.Empty) + ".",
                    severity = 0.2f
                };
            }
        }
}
