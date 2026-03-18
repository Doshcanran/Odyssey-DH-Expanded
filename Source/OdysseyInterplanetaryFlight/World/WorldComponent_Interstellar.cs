using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InterstellarOdyssey
{
        public class WorldComponent_Interstellar : WorldComponent
        {
            public List<OrbitalNode> nodes = new List<OrbitalNode>();
            public List<ShipTransitRecord> activeTravels = new List<ShipTransitRecord>();
            public bool generated;

            public WorldComponent_Interstellar(World world) : base(world)
            {
            }

            public override void FinalizeInit(bool fromLoad)
            {
                base.FinalizeInit(fromLoad);
                GenerateIfNeeded();
            }

            public override void WorldComponentTick()
            {
                base.WorldComponentTick();

                if (Find.TickManager == null)
                    return;

                for (int i = activeTravels.Count - 1; i >= 0; i--)
                {
                    ShipTransitRecord travel = activeTravels[i];
                    if (travel.stage != InterstellarTransitStage.InTransit)
                        continue;

                    ShipTransitEventUtility.TryProcessEvent(this, travel);

                    if (Find.TickManager.TicksGame >= travel.arrivalTick)
                        Arrive(travel);
                }
            }

            public override void ExposeData()
            {
                base.ExposeData();
                Scribe_Values.Look(ref generated, "generated", false);
                Scribe_Collections.Look(ref nodes, "nodes", LookMode.Deep);
                Scribe_Collections.Look(ref activeTravels, "activeTravels", LookMode.Deep);

                if (nodes == null) nodes = new List<OrbitalNode>();
                if (activeTravels == null) activeTravels = new List<ShipTransitRecord>();
            }

            public void GenerateIfNeeded()
            {
                if (generated && nodes.Count > 0)
                    return;

                nodes.Clear();

                string planetName = Find.World != null && Find.World.info != null
                    ? Find.World.info.name
                    : "RimWorld";

                nodes.Add(new OrbitalNode("homeworld", planetName, OrbitalNodeType.Planet, 0f, 75f));
                nodes.Add(new OrbitalNode("ares", "Ares", OrbitalNodeType.Planet, 160f, 130f));
                nodes.Add(new OrbitalNode("nivalis", "Nivalis", OrbitalNodeType.Planet, 320f, 185f));
                nodes.Add(new OrbitalNode("station", "Орбитальная станция", OrbitalNodeType.Station, 75f, 45f));
                nodes.Add(new OrbitalNode("belt", "Пояс астероидов", OrbitalNodeType.AsteroidBelt, 235f, 230f));
                nodes.Add(new OrbitalNode("hekate", "Геката", OrbitalNodeType.Asteroid, 25f, 205f));

                generated = true;
            }

            public bool IsShipTravelling(Thing ship)
            {
                if (ship == null)
                    return false;

                return activeTravels.Any(t => t.shipThingId == ship.thingIDNumber && t.stage != InterstellarTransitStage.None);
            }

            public OrbitalNode GetCurrentNodeForShip(Thing ship)
            {
                if (ship == null)
                    return GetNodeById("homeworld") ?? nodes.FirstOrDefault();

                ShipTransitRecord record = activeTravels.FirstOrDefault(t => t.shipThingId == ship.thingIDNumber);
                if (record != null)
                    return GetNodeById(record.destinationId) ?? GetNodeById(record.sourceId);

                return GetNodeById("homeworld") ?? nodes.FirstOrDefault();
            }

            public OrbitalNode GetNodeById(string id)
            {
                return nodes.FirstOrDefault(n => n.id == id);
            }

            public string ResolveNodeLabel(OrbitalNode node)
            {
                return node != null ? node.label : "Неизвестно";
            }

            public IEnumerable<ShipTransitRecord> GetLandingReadyTravels()
            {
                return activeTravels.Where(t => t.stage == InterstellarTransitStage.AwaitingLanding);
            }

            public bool StartTravel(Thing shipAnchor, OrbitalNode destination)
            {
                if (shipAnchor == null || destination == null)
                    return false;

                if (IsShipTravelling(shipAnchor))
                {
                    Messages.Message("Корабль уже находится в перелёте.", MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                OrbitalNode current = GetCurrentNodeForShip(shipAnchor);
                if (current != null && current.id == destination.id)
                {
                    Messages.Message("Корабль уже находится у этой цели.", MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                ShipValidationReport validation = ShipValidationUtility.ValidateForLaunch(shipAnchor);
                if (!validation.CanLaunch)
                {
                    Find.WindowStack.Add(new Dialog_MessageBox(validation.ToUserText()));
                    return false;
                }

                if (!ShipCaptureUtility.TryCollectShipCluster(shipAnchor, out ShipClusterData launchCluster))
                {
                    Messages.Message("Не удалось определить состав корабля перед стартом.", MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                ShipPropulsionReport propulsion = ShipPropulsionUtility.Evaluate(launchCluster, current, destination);
                if (!propulsion.hasEnoughThrust)
                {
                    Messages.Message("Недостаточная тяга: корабль слишком тяжёлый для текущих двигателей.", MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                if (!propulsion.hasEnoughFuel)
                {
                    Messages.Message("Недостаточно топлива для маршрута. Нужно " + propulsion.fuelNeeded.ToString("0.#") + ", доступно " + propulsion.totalFuel.ToString("0.#") + ".", MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                ShipPropulsionUtility.ConsumeFuel(launchCluster, propulsion.fuelNeeded);

                if (!ShipCaptureUtility.TryCaptureAndDespawnShip(shipAnchor, current != null ? current.id : "homeworld", out ShipSnapshot snapshot))
                {
                    Messages.Message("Не удалось захватить корабль для перелёта.", MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                float days = propulsion.travelDays > 0f ? propulsion.travelDays : Mathf.Max(0.2f, OrbitalMath.Distance(current, destination) / 45f);
                int durationTicks = Mathf.Max(2500, Mathf.RoundToInt(days * GenDate.TicksPerDay));

                ShipTransitRecord record = new ShipTransitRecord
                {
                    shipThingId = shipAnchor.thingIDNumber,
                    shipLabel = shipAnchor.LabelCap,
                    shipDefName = shipAnchor.def.defName,
                    sourceId = current != null ? current.id : "homeworld",
                    destinationId = destination.id,
                    departureTick = Find.TickManager.TicksGame,
                    arrivalTick = Find.TickManager.TicksGame + durationTicks,
                    stage = InterstellarTransitStage.InTransit,
                    snapshot = snapshot,
                    preferredLandingMode = ShipLandingMode.Precise
                };

                ShipTransitEventUtility.ScheduleNextEvent(record, Find.TickManager.TicksGame);
                activeTravels.Add(record);
                Messages.Message("Начат межпланетный перелёт: " + record.shipLabel + " → " + ResolveNodeLabel(destination) + ". Израсходовано топлива: " + propulsion.fuelNeeded.ToString("0.#"), MessageTypeDefOf.PositiveEvent, false);
                return true;
            }

            public bool TryLandShip(ShipTransitRecord record, Map map, ShipLandingMode mode)
            {
                if (record == null || map == null || record.snapshot == null)
                    return false;

                if (!ShipLandingUtility.TryFindLandingCenter(record.snapshot, map, mode, out IntVec3 center))
                    center = map.Center;

                if (!ShipLandingUtility.TryRestoreShip(record.snapshot, map, center, mode, out Thing restoredAnchor))
                    return false;

                ShipLandingUtility.SpawnTransitLoot(record, map, center);

                record.stage = InterstellarTransitStage.None;
                activeTravels.Remove(record);

                OrbitalNode destination = GetNodeById(record.destinationId);
                Messages.Message("Корабль " + (record.shipLabel ?? "без названия") + " совершил посадку у цели " + ResolveNodeLabel(destination) + " [" + ResolveLandingModeLabel(mode) + "].", MessageTypeDefOf.PositiveEvent, false);
                return true;
            }

            public string ResolveLandingModeLabel(ShipLandingMode mode)
            {
                switch (mode)
                {
                    case ShipLandingMode.Emergency: return "аварийная";
                    case ShipLandingMode.OrbitalDrop: return "орбитальный дроп";
                    case ShipLandingMode.UnpreparedSurface: return "неподготовленная поверхность";
                    case ShipLandingMode.StationDocking: return "стыковка";
                    default: return "точная";
                }
            }

            private void Arrive(ShipTransitRecord record)
            {
                record.stage = InterstellarTransitStage.AwaitingLanding;
                if (record.snapshot != null)
                    record.snapshot.currentNodeId = record.destinationId;

                OrbitalNode destination = GetNodeById(record.destinationId);
                if (destination != null && destination.id != "homeworld")
                    OrbitalNodeMapUtility.ResolveOrCreateMapForNode(destination);

                Messages.Message("Корабль вышел на орбиту цели: " + ResolveNodeLabel(destination) + ". Рекомендуемый режим посадки: " + ResolveLandingModeLabel(record.preferredLandingMode) + ".", MessageTypeDefOf.PositiveEvent, false);
            }
        }
}
