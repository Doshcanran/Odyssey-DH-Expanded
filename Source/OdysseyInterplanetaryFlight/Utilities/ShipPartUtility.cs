using RimWorld;
using Verse;

namespace InterstellarOdyssey
{
    public static class ShipPartUtility
    {
        public static bool HasRole(Thing thing, ShipPartRole role)
        {
            return thing != null && HasRole(thing.def, role);
        }

        public static bool HasRole(ThingDef def, ShipPartRole role)
        {
            if (def == null)
                return false;

            ShipPartRoleExtension ext = def.GetModExtension<ShipPartRoleExtension>();
            return ext?.roles != null && ext.roles.Contains(role);
        }

        public static bool IsShipStructure(Thing thing)
        {
            if (thing == null || thing.def == null)
                return false;

            if (thing.def.category != ThingCategory.Building)
                return false;

            return HasRole(thing, ShipPartRole.Structure) || HasRole(thing, ShipPartRole.HullBoundary);
        }

        public static bool IsCore(Thing thing) => HasRole(thing, ShipPartRole.Core);
        public static bool IsEngine(Thing thing) => HasRole(thing, ShipPartRole.Engine);
        public static bool IsNavigationConsole(Thing thing) => HasRole(thing, ShipPartRole.NavigationConsole);

        public static bool IsFuelBearingPart(Thing thing)
        {
            if (thing == null || thing.def == null || thing.TryGetComp<CompRefuelable>() == null)
                return false;

            return HasRole(thing, ShipPartRole.FuelTank);
        }

        public static bool IsHullBoundary(Thing thing)
        {
            if (thing == null || thing.def == null || thing.def.category != ThingCategory.Building)
                return false;

            return HasRole(thing, ShipPartRole.HullBoundary);
        }

        public static bool IsShipFloor(TerrainDef terrain)
        {
            if (terrain == null)
                return false;

            ShipPartRoleExtension ext = terrain.GetModExtension<ShipPartRoleExtension>();
            return ext != null && ext.isShipFloor;
        }

        public static float GetEngineThrust(Thing thing)
        {
            if (thing?.def == null || !HasRole(thing, ShipPartRole.Engine))
                return 0f;

            ShipPartRoleExtension ext = thing.def.GetModExtension<ShipPartRoleExtension>();
            if (ext == null || ext.engineThrust <= 0f)
            {
                InterstellarDiagnostics.RecordWarning(
                    "Defs",
                    "Не задана тяга двигателя",
                    "Для двигателя не задан engineThrust. Двигатель не будет давать тягу.",
                    thing.def.defName);
                return 0f;
            }

            return ext.engineThrust;
        }
    }
}
