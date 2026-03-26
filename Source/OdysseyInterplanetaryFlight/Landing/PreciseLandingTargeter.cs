using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
    /// <summary>
    /// Второй этап посадки:
    /// 1) на мировом шаре игрок выбирает тайл;
    /// 2) генерируется settlement/map;
    /// 3) игрок уже на карте выбирает точную клетку посадки.
    /// </summary>
    public static class PreciseLandingTargeter
    {
        public static void Begin(
            WorldComponent_Interstellar data,
            ShipTransitRecord record,
            Map map,
            OrbitalNode destination,
            int tile,
            ShipLandingMode mode)
        {
            if (data == null || record == null || map == null || record.snapshot == null)
                return;

            Current.Game.CurrentMap = map;
            CameraJumper.TryJump(map.Center, map);

            Messages.Message(
                "Выберите точное место посадки на карте.",
                MessageTypeDefOf.TaskCompletion,
                false);

            Find.DesignatorManager.Select(
                new Designator_PreciseShipLanding(data, record, map, destination, tile, mode));
        }
    }

    public sealed class Designator_PreciseShipLanding : Designator
    {
        private readonly WorldComponent_Interstellar data;
        private readonly ShipTransitRecord record;
        private readonly Map map;
        private readonly OrbitalNode destination;
        private readonly int tile;
        private readonly ShipLandingMode mode;

        private static readonly Color ValidColor = new Color(0.20f, 1.00f, 0.20f, 0.90f);
        private static readonly Color InvalidColor = new Color(1.00f, 0.20f, 0.20f, 0.90f);

        public Designator_PreciseShipLanding(
            WorldComponent_Interstellar data,
            ShipTransitRecord record,
            Map map,
            OrbitalNode destination,
            int tile,
            ShipLandingMode mode)
        {
            this.data = data;
            this.record = record;
            this.map = map;
            this.destination = destination;
            this.tile = tile;
            this.mode = mode;

            defaultLabel = "Точная посадка";
            defaultDesc = "Выбери точную клетку посадки корабля на карте.";
            useMouseIcon = false;
            soundSucceeded = SoundDefOf.Tick_High;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (map == null || record == null || record.snapshot == null)
                return "Нет данных для посадки.";

            if (!c.IsValid || !c.InBounds(map))
                return false;

            if (ShipLandingUtility.CanLandPreciselyAt(record.snapshot, map, c, out string reason))
                return AcceptanceReport.WasAccepted;

            return reason.NullOrEmpty() ? "Нельзя посадить корабль здесь." : reason;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (!ShipLandingUtility.CanLandPreciselyAt(record.snapshot, map, c, out string reason))
            {
                Messages.Message(
                    reason.NullOrEmpty() ? "Нельзя посадить корабль в этой точке." : reason,
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            Find.DesignatorManager.Deselect();

            bool landed = data.FinalizeLandingOnGeneratedMap(record, map, tile, c, mode);
            if (!landed)
            {
                Messages.Message("Посадка не удалась.", MessageTypeDefOf.RejectInput, false);
                PreciseLandingTargeter.Begin(data, record, map, destination, tile, mode);
            }
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();

            if (map == null || record == null || record.snapshot == null)
                return;

            IntVec3 mouseCell = UI.MouseCell();
            if (!mouseCell.IsValid || !mouseCell.InBounds(map))
                return;

            bool valid = ShipLandingUtility.CanLandPreciselyAt(record.snapshot, map, mouseCell, out string reason);
            List<IntVec3> occupiedCells = ShipLandingUtility.GetOccupiedCellsAt(record.snapshot, mouseCell);

            GenDraw.DrawFieldEdges(occupiedCells, valid ? ValidColor : InvalidColor);
            ShipLandingUtility.DrawGhostPreview(record.snapshot, mouseCell, valid);
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);

            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                Find.DesignatorManager.Deselect();
                Messages.Message(
                    "Выбор места посадки отменён. Корабль всё ещё ожидает точку входа.",
                    MessageTypeDefOf.NeutralEvent,
                    false);
                ev.Use();
            }
        }
    }
}
