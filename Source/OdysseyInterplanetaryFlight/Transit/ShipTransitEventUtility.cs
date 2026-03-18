using RimWorld;
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

            string impact = !string.IsNullOrEmpty(transitEvent.impactSummary) ? " Последствия: " + transitEvent.impactSummary : string.Empty;
            Messages.Message(transitEvent.title + ": " + transitEvent.description + impact, MessageTypeDefOf.NeutralEvent, false);
            InterstellarDiagnostics.RecordInfo("TransitEvent", transitEvent.title, transitEvent.description, transitEvent.impactSummary);
            return true;
        }

        public static string DescribeConsequences(ShipTransitEvent transitEvent)
        {
            if (transitEvent == null)
                return "Последствия не зафиксированы.";

            if (!string.IsNullOrEmpty(transitEvent.impactSummary))
                return transitEvent.impactSummary;

            return "Без прямых последствий.";
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
            record.preferredLandingMode = ShipLandingMode.Emergency;

            return new ShipTransitEvent
            {
                type = TransitEventType.EngineBreakdown,
                tick = currentTick,
                title = "Поломка двигателя",
                description = "Тяга временно упала. Прибытие задержано примерно на " + (delay / 2500f).ToString("0.0") + " д.",
                impactSummary = "Задержка +" + (delay / 2500f).ToString("0.0") + " д.; рекомендуется аварийная посадка.",
                severity = 0.7f,
                changesRecommendedLandingMode = true,
                recommendedLandingMode = ShipLandingMode.Emergency,
                delayTicks = delay
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
                impactSummary = "Задержка +" + (delay / 2500f).ToString("0.0") + " д.; запас прочности снижен.",
                severity = 0.35f,
                delayTicks = delay
            };
        }

        private static ShipTransitEvent ApplySolarStorm(ShipTransitRecord record, int currentTick)
        {
            int delay = Mathf.RoundToInt(0.16f * GenDate.TicksPerDay);
            record.arrivalTick += delay;
            record.travelDisruption += 0.16f;
            if (record.preferredLandingMode == ShipLandingMode.Precise)
                record.preferredLandingMode = ShipLandingMode.UnpreparedSurface;

            return new ShipTransitEvent
            {
                type = TransitEventType.SolarStorm,
                tick = currentTick,
                title = "Солнечная буря",
                description = "Корабль вошёл в зону вспышки и был вынужден снизить ход.",
                impactSummary = "Задержка +" + (delay / 2500f).ToString("0.0") + " д.; точная посадка менее надёжна.",
                severity = 0.55f,
                changesRecommendedLandingMode = true,
                recommendedLandingMode = record.preferredLandingMode,
                delayTicks = delay
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
                impactSummary = "Задержка +" + (delay / 2500f).ToString("0.0") + " д.; маршрут нестабилен.",
                severity = 0.8f,
                changesRecommendedLandingMode = true,
                recommendedLandingMode = ShipLandingMode.Emergency,
                delayTicks = delay
            };
        }

        private static ShipTransitEvent ApplyPirateSignal(ShipTransitRecord record, int currentTick)
        {
            if (record.preferredLandingMode == ShipLandingMode.Precise)
                record.preferredLandingMode = ShipLandingMode.OrbitalDrop;

            return new ShipTransitEvent
            {
                type = TransitEventType.PirateSignal,
                tick = currentTick,
                title = "Пиратский сигнал",
                description = "Зафиксирован подозрительный источник на дальней дистанции. Возможна горячая посадка.",
                impactSummary = "Рекомендуется быстрый вход и рассредоточение экипажа.",
                severity = 0.45f,
                changesRecommendedLandingMode = true,
                recommendedLandingMode = record.preferredLandingMode
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
                impactSummary = "После посадки будет выгружен дополнительный salvage.",
                severity = 0.2f,
                salvageSteel = steel,
                salvageComponents = components
            };
        }
    }
}
