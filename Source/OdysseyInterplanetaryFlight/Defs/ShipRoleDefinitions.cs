using System.Collections.Generic;
using Verse;

namespace InterstellarOdyssey
{
    public enum ShipPartRole
    {
        None = 0,
        Core = 1,
        Engine = 2,
        FuelTank = 3,
        NavigationConsole = 4,
        HullBoundary = 5,
        Structure = 6
    }

    public class ShipPartRoleExtension : DefModExtension
    {
        public List<ShipPartRole> roles;
        public bool isShipFloor;
        public float engineThrust = -1f;
    }
}
