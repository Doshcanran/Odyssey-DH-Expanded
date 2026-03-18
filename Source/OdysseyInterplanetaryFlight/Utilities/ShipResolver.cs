using RimWorld;
using Verse;

namespace InterstellarOdyssey
{
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