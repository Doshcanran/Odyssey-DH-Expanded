using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace InterstellarOdyssey
{
    public class MapComponent_VoidSpace : MapComponent
    {
        private const int TickInterval = 300;

        public MapComponent_VoidSpace(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager == null)
                return;

            if (Find.TickManager.TicksGame % TickInterval != 0)
                return;

            RemoveWildAnimals();
            DamageExposedPawns();
        }

        private void RemoveWildAnimals()
        {
            List<Pawn> allPawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            foreach (Pawn pawn in allPawns)
            {
                if (pawn == null || pawn.Destroyed || !pawn.Spawned)
                    continue;

                if (pawn.RaceProps != null && pawn.RaceProps.Animal
                    && (pawn.Faction == null || pawn.Faction != Faction.OfPlayer))
                {
                    pawn.Destroy();
                }
            }
        }

        private void DamageExposedPawns()
        {
            TerrainDef voidFloor = DefDatabase<TerrainDef>.GetNamedSilentFail("IO_VoidFloor");
            if (voidFloor == null)
                return;

            // Используем Bite как ближайший аналог физического урона — в 1.6 нет DamageDefOf.Hypothermia
            DamageDef damageDef = DamageDefOf.Bite;

            List<Pawn> allPawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            foreach (Pawn pawn in allPawns)
            {
                if (pawn == null || pawn.Destroyed || !pawn.Spawned || pawn.Dead)
                    continue;

                if (pawn.Faction != Faction.OfPlayer)
                    continue;

                TerrainDef terrain = map.terrainGrid?.TerrainAt(pawn.Position);
                if (terrain != voidFloor)
                    continue;

                if (map.roofGrid?.RoofAt(pawn.Position) != null)
                    continue;

                pawn.TakeDamage(new DamageInfo(damageDef, 8f));
            }
        }
    }
}
