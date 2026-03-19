using Verse;
using RimWorld;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Размещает корабль в центре карты вакуума,
    /// применяет условия открытого космоса.
    /// </summary>
    public class GenStep_IO_PlaceShipOnVoidMap : GenStep
    {
        public override int SeedPart => 0x494F5053; // "IOPS"

        public override void Generate(Map map, GenStepParams parms)
        {
            ShipSnapshot snapshot = VoidMapUtility.PendingSnapshot;
            if (snapshot == null)
            {
                Log.Warning("[InterstellarOdyssey] GenStep_IO_PlaceShipOnVoidMap: нет снапшота.");
                return;
            }

            // 1. Добавляем MapComponent вакуума (если ещё нет)
            if (map.GetComponent<MapComponent_VoidSpace>() == null)
                map.components.Add(new MapComponent_VoidSpace(map));

            // 2. Размещаем корабль в центре карты
            Thing restoredAnchor = null;
            ShipLandingUtility.TryRestoreShip(snapshot, map, map.Center, ShipLandingMode.Precise, out restoredAnchor);

            // 3. Добавляем GameCondition вакуума — блокирует погоду, снижает температуру
            GameConditionDef condDef = DefDatabase<GameConditionDef>.GetNamedSilentFail("IO_VoidSpaceCondition");
            if (condDef != null && !map.gameConditionManager.ConditionIsActive(condDef))
            {
                GameCondition cond = GameConditionMaker.MakeCondition(condDef, int.MaxValue);
                map.gameConditionManager.RegisterCondition(cond);
            }

            Log.Message("[InterstellarOdyssey] Корабль размещён на карте вакуума. anchor="
                + (restoredAnchor != null ? restoredAnchor.Label : "null"));
        }
    }
}
