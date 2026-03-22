using Verse;

namespace InterstellarOdyssey
{
    public static class ShipTransitEventUtility
    {
        public static void ScheduleNextEvent(ShipTransitRecord record, int currentTick)
        {
            if (record == null)
                return;

            record.nextEventTick = 0;
            if (record.eventLog != null)
                record.eventLog.Clear();
        }

        public static void TryProcessEvent(WorldComponent_Interstellar data, ShipTransitRecord record)
        {
        }
    }
}
