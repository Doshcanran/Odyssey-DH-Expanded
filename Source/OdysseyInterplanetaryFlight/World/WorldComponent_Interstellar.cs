using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ShipPropulsionUtility = InterstellarOdyssey.ShipPropulsionUtility;

namespace InterstellarOdyssey
{
    public class WorldComponent_Interstellar : WorldComponent
    {
        private const int MaxDiagnostics = 160;

        public List<OrbitalNode> nodes = new List<OrbitalNode>();
        public List<ShipTransitRecord> activeTravels = new List<ShipTransitRecord>();
        public List<ShipLocationRecord> shipLocations = new List<ShipLocationRecord>();
        public List<InterstellarDiagnosticEntry> diagnostics = new List<InterstellarDiagnosticEntry>();
        public bool generated;
        public GalaxyWorldConfiguration galaxyConfig = new GalaxyWorldConfiguration();
        public string selectedGalaxyId = "galaxy_0";
        public string selectedSolarSystemId = "system_0";

        public WorldComponent_Interstellar(World world) : base(world)
        {
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            GenerateIfNeeded();
            EnsureLocationRecords();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (Find.TickManager == null)
                return;

            for (int i = activeTravels.Count - 1; i >= 0; i--)
            {
                ShipTransitRecord travel = activeTravels[i];
                if (travel == null || travel.stage != InterstellarTransitStage.InTransit)
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
            Scribe_Collections.Look(ref shipLocations, "shipLocations", LookMode.Deep);
            Scribe_Collections.Look(ref diagnostics, "diagnostics", LookMode.Deep);
            Scribe_Deep.Look(ref galaxyConfig, "galaxyConfig");
            Scribe_Values.Look(ref selectedGalaxyId, "selectedGalaxyId", "galaxy_0");
            Scribe_Values.Look(ref selectedSolarSystemId, "selectedSolarSystemId", "system_0");

            if (nodes == null) nodes = new List<OrbitalNode>();
            if (activeTravels == null) activeTravels = new List<ShipTransitRecord>();
            if (shipLocations == null) shipLocations = new List<ShipLocationRecord>();
            if (diagnostics == null) diagnostics = new List<InterstellarDiagnosticEntry>();
            if (galaxyConfig == null) galaxyConfig = GalaxyConfigUtility.CreateDefaultConfiguration();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                GalaxyConfigUtility.EnsureConsistency(galaxyConfig);
                if (string.IsNullOrEmpty(selectedGalaxyId))
                    selectedGalaxyId = galaxyConfig.galaxies.FirstOrDefault()?.id ?? "galaxy_0";
                EnsureLocationRecords();
            }
        }

        public void GenerateIfNeeded()
        {
            if (generated && nodes != null && nodes.Count > 0)
                return;

            if (galaxyConfig == null || galaxyConfig.galaxies == null || galaxyConfig.galaxies.Count == 0)
            {
                galaxyConfig = CloneConfig(InterstellarOdysseyMod.PendingGalaxyConfig) ?? GalaxyConfigUtility.CreateDefaultConfiguration();
            }

            GalaxyConfigUtility.EnsureConsistency(galaxyConfig);
            PlanetArchiveUtility.PrepareArchivesForAllPlanets(galaxyConfig);
            nodes = GalaxyOrbitalNodeFactory.CreateNodes(galaxyConfig);
            if (nodes == null || nodes.Count == 0)
                nodes = DefaultOrbitalNodeFactory.CreateDefaultNodes();

            selectedGalaxyId = ResolveInitialGalaxyId();
            selectedSolarSystemId = GetGalaxyById(selectedGalaxyId)?.solarSystemId ?? "system_0";
            generated = true;
        }

        public GalaxyDefinition GetGalaxyById(string galaxyId)
        {
            return galaxyConfig?.galaxies?.FirstOrDefault(g => g != null && g.id == galaxyId);
        }

        public IEnumerable<GalaxyDefinition> GetGalaxies()
        {
            return galaxyConfig?.galaxies ?? Enumerable.Empty<GalaxyDefinition>();
        }

        public IEnumerable<OrbitalNode> GetNodesForGalaxy(string galaxyId)
        {
            GenerateIfNeeded();

            if (string.IsNullOrEmpty(galaxyId))
                galaxyId = selectedGalaxyId;

            return nodes.Where(n => n != null && n.galaxyId == galaxyId);
        }

        public IEnumerable<OrbitalNode> GetNodesForSelectedGalaxy()
        {
            return GetNodesForGalaxy(selectedGalaxyId);
        }

        public IEnumerable<PlanetDefinition_IO> GetPlanetsForGalaxy(string galaxyId)
        {
            GalaxyDefinition galaxy = GetGalaxyById(galaxyId);
            return galaxy?.planets ?? Enumerable.Empty<PlanetDefinition_IO>();
        }

        public void SelectGalaxy(string galaxyId)
        {
            if (string.IsNullOrEmpty(galaxyId))
                return;

            if (GetGalaxyById(galaxyId) == null)
                return;

            selectedGalaxyId = galaxyId;
            selectedSolarSystemId = GetGalaxyById(galaxyId)?.solarSystemId ?? selectedSolarSystemId;
        }

        public string GetDefaultHomeNodeId()
        {
            GenerateIfNeeded();
            PlanetDefinition_IO startPlanet = GalaxyConfigUtility.GetStartPlanet(galaxyConfig);
            return startPlanet?.id ?? "homeworld";
        }

        public string ResolveInitialGalaxyId()
        {
            PlanetDefinition_IO startPlanet = GalaxyConfigUtility.GetStartPlanet(galaxyConfig);
            OrbitalNode node = nodes.FirstOrDefault(n => n != null && n.id == startPlanet?.id);
            return node?.galaxyId ?? galaxyConfig.galaxies.FirstOrDefault()?.id ?? "galaxy_0";
        }

        public bool IsShipTravelling(Thing ship)
        {
            if (ship == null)
                return false;

            return activeTravels.Any(t => t != null && t.shipThingId == ship.thingIDNumber && t.stage != InterstellarTransitStage.None);
        }

        public OrbitalNode GetCurrentNodeForShip(Thing ship)
        {
            string nodeId = GetCurrentNodeIdForShip(ship);
            return GetNodeById(nodeId) ?? GetNodeById(GetDefaultHomeNodeId()) ?? nodes.FirstOrDefault();
        }

        public string GetCurrentNodeIdForShip(Thing ship)
        {
            if (ship == null)
                return GetDefaultHomeNodeId();

            ShipTransitRecord record = activeTravels.FirstOrDefault(t => t != null && t.shipThingId == ship.thingIDNumber);
            if (record != null)
            {
                if (record.stage == InterstellarTransitStage.AwaitingLanding)
                    return !string.IsNullOrEmpty(record.destinationId) ? record.destinationId : record.sourceId;

                if (!string.IsNullOrEmpty(record.sourceId))
                    return record.sourceId;
            }

            ShipLocationRecord location = FindLocationRecord(ship);
            if (location != null && !string.IsNullOrEmpty(location.currentNodeId))
                return location.currentNodeId;

            EnsureHomeworldLocation(ship);
            return GetDefaultHomeNodeId();
        }

        public OrbitalNode GetNodeById(string id)
        {
            GenerateIfNeeded();
            return nodes.FirstOrDefault(n => n != null && n.id == id);
        }

        public string ResolveNodeLabel(OrbitalNode node)
        {
            if (node == null)
                return "Неизвестно";

            if (!string.IsNullOrEmpty(node.label))
                return node.label;

            return node.id ?? "Неизвестно";
        }

        public IEnumerable<ShipTransitRecord> GetLandingReadyTravels()
        {
            return activeTravels.Where(t => t != null && t.stage == InterstellarTransitStage.AwaitingLanding);
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

            string currentNodeId = GetCurrentNodeIdForShip(shipAnchor);
            OrbitalNode current = GetNodeById(currentNodeId) ?? GetNodeById(GetDefaultHomeNodeId()) ?? nodes.FirstOrDefault();
            if (current != null && current.id == destination.id)
            {
                Messages.Message("Корабль уже находится у этой цели.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            string launchDiagnostic = InterstellarDiagnostics.BuildLaunchDiagnosticReport(this, shipAnchor, current, destination);
            AddDiagnostic("Launch", "Предполетная диагностика", shipAnchor.LabelCap, launchDiagnostic, InterstellarDiagnosticSeverity.Info);

            ShipValidationReport validation = ShipValidationUtility.ValidateForLaunch(shipAnchor);
            if (!validation.CanLaunch)
            {
                AddDiagnostic("Launch", "Старт отклонён", "Валидация корабля не пройдена.", validation.ToUserText(), InterstellarDiagnosticSeverity.Warning);
                Find.WindowStack.Add(new Dialog_MessageBox(validation.ToUserText()));
                return false;
            }

            if (!ShipCaptureUtility.TryCollectShipCluster(shipAnchor, out ShipClusterData launchCluster))
            {
                AddDiagnostic("Launch", "Старт отклонён", "Не удалось определить состав корабля перед стартом.", launchDiagnostic, InterstellarDiagnosticSeverity.Error);
                Messages.Message("Не удалось определить состав корабля перед стартом.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            ShipPropulsionReport propulsion = ShipPropulsionUtility.Evaluate(launchCluster, current, destination);
            if (!propulsion.hasEnoughThrust)
            {
                AddDiagnostic("Launch", "Старт отклонён", "Недостаточная тяга.", launchDiagnostic, InterstellarDiagnosticSeverity.Warning);
                Messages.Message("Недостаточная тяга: корабль слишком тяжёлый для текущих двигателей.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!propulsion.hasEnoughFuel)
            {
                AddDiagnostic("Launch", "Старт отклонён", "Недостаточно топлива.", launchDiagnostic, InterstellarDiagnosticSeverity.Warning);
                Messages.Message("Недостаточно топлива для маршрута. Нужно " + propulsion.fuelNeeded.ToString("0.#") + ", доступно " + propulsion.totalFuel.ToString("0.#") + ".", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            Map sourceMap = shipAnchor.Map;
            IntVec3 sourceAnchorCell = shipAnchor.Position;
            ShipSnapshot snapshot = null;
            ShipTransitRecord record = null;
            Dictionary<Thing, float> fuelState = ShipPropulsionUtility.SnapshotFuelState(launchCluster);
            bool fuelCommitted = false;
            bool travelAdded = false;
            bool intergalactic = current != null && !string.Equals(current.galaxyId, destination.galaxyId, StringComparison.Ordinal);

            try
            {
                EnsureCurrentNodeForShip(shipAnchor, currentNodeId);

                if (!ShipCaptureUtility.TryCaptureAndDespawnShip(shipAnchor, currentNodeId, out snapshot))
                {
                    AddDiagnostic("Launch", "Старт отклонён", "Не удалось захватить корабль для перелёта.", launchDiagnostic, InterstellarDiagnosticSeverity.Error);
                    Messages.Message("Не удалось захватить корабль для перелёта.", MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                ShipPropulsionUtility.ConsumeFuel(launchCluster, propulsion.fuelNeeded);
                fuelCommitted = true;

                int durationTicks;
                if (intergalactic)
                {
                    durationTicks = GenDate.TicksPerDay;
                }
                else
                {
                    float days = propulsion.travelDays > 0f ? propulsion.travelDays : Mathf.Max(0.2f, OrbitalMath.Distance(current, destination) / 45f);
                    durationTicks = Mathf.Max(2500, Mathf.RoundToInt(days * GenDate.TicksPerDay));
                }

                record = new ShipTransitRecord
                {
                    shipThingId = shipAnchor.thingIDNumber,
                    shipLabel = shipAnchor.LabelCap,
                    shipDefName = shipAnchor.def.defName,
                    sourceId = currentNodeId,
                    destinationId = destination.id,
                    departureTick = Find.TickManager.TicksGame,
                    arrivalTick = Find.TickManager.TicksGame + durationTicks,
                    stage = InterstellarTransitStage.InTransit,
                    snapshot = snapshot,
                    preferredLandingMode = ShipLandingMode.Precise,
                    intergalacticTravel = intergalactic,
                    sourceGalaxyId = current?.galaxyId,
                    destinationGalaxyId = destination.galaxyId
                };

                ShipTransitEventUtility.ScheduleNextEvent(record, Find.TickManager.TicksGame);
                activeTravels.Add(record);
                travelAdded = true;

                if (!VoidMapUtility.CreateVoidMap(record))
                {
                    AddDiagnostic("Launch", "Предупреждение", "Не удалось создать карту вакуума. Перелёт продолжается без неё.", launchDiagnostic, InterstellarDiagnosticSeverity.Warning);
                    Messages.Message("Карта вакуума не создана, но перелёт начат.", MessageTypeDefOf.NeutralEvent, false);
                }

                string message = intergalactic
                    ? "Начат тестовый межгалактический перелёт: " + record.shipLabel + " → " + ResolveNodeLabel(destination) + ". Время полёта: 1 день."
                    : "Начат межпланетный перелёт: " + record.shipLabel + " → " + ResolveNodeLabel(destination) + ". Израсходовано топлива: " + propulsion.fuelNeeded.ToString("0.#");

                AddDiagnostic("Launch", "Перелёт начат", record.shipLabel + " → " + ResolveNodeLabel(destination), launchDiagnostic, InterstellarDiagnosticSeverity.Info);
                Messages.Message(message, MessageTypeDefOf.PositiveEvent, false);
                return true;
            }
            catch (Exception ex)
            {
                if (travelAdded && record != null)
                    activeTravels.Remove(record);

                if (record != null && record.voidMapTile >= 0)
                {
                    try { VoidMapUtility.DestroyVoidMap(record); }
                    catch (Exception vmEx) { Log.Warning("[InterstellarOdyssey] Не удалось удалить карту вакуума при откате: " + vmEx.Message); }
                }

                Log.Error("[InterstellarOdyssey] StartTravel failed, performing rollback: " + ex);
                AddDiagnostic("Launch", "Ошибка старта", ex.Message, ex.ToString(), InterstellarDiagnosticSeverity.Error);

                if (fuelCommitted)
                {
                    try
                    {
                        ShipPropulsionUtility.RestoreFuelState(fuelState);
                    }
                    catch (Exception fuelEx)
                    {
                        AddDiagnostic("Launch", "Сбой отката топлива", fuelEx.Message, fuelEx.ToString(), InterstellarDiagnosticSeverity.Error);
                    }
                }

                if (snapshot != null && sourceMap != null)
                {
                    try
                    {
                        snapshot.anchorCell = sourceAnchorCell;
                        if (ShipLandingUtility.TryRestoreShip(snapshot, sourceMap, sourceAnchorCell, ShipLandingMode.Precise, out Thing restoredAnchor))
                            SetCurrentNodeForShip(restoredAnchor, currentNodeId, shipAnchor.def.defName, shipAnchor.LabelCap, shipAnchor.thingIDNumber);
                    }
                    catch (Exception restoreEx)
                    {
                        AddDiagnostic("Launch", "Сбой отката корабля", restoreEx.Message, restoreEx.ToString(), InterstellarDiagnosticSeverity.Error);
                    }
                }

                Messages.Message("Перелёт прерван из-за ошибки. Выполнен откат.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
        }

        public bool TryLandShip(ShipTransitRecord record, Map map, ShipLandingMode mode)
        {
            if (record == null || map == null || record.snapshot == null)
                return false;

            if (!ShipLandingUtility.IsModeAllowedForDestination(mode, GetNodeById(record.destinationId), out string reason))
            {
                Messages.Message(reason ?? "Этот режим посадки недоступен.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (VoidMapUtility.HasVoidMap(record))
            {
                bool recaptured = VoidMapUtility.RecaptureShipFromVoidMap(record);
                if (!recaptured)
                {
                    AddDiagnostic("Landing", "Посадка отменена", "Не удалось захватить актуальное состояние корабля с карты вакуума.", record.shipLabel, InterstellarDiagnosticSeverity.Error);
                    Messages.Message("Посадка отменена: не удалось захватить актуальное состояние корабля с карты вакуума.", MessageTypeDefOf.RejectInput, false);
                    return false;
                }
            }

            if (!ShipLandingUtility.TryFindLandingCenter(record.snapshot, map, mode, out IntVec3 center))
                center = map.Center;

            if (!ShipLandingUtility.TryRestoreShip(record.snapshot, map, center, mode, out Thing restoredAnchor))
            {
                AddDiagnostic("Landing", "Посадка не удалась", "Восстановление корабля прервано.", "Map=" + (map.Parent != null ? map.Parent.LabelCap : map.ToString()) + " mode=" + mode, InterstellarDiagnosticSeverity.Error);
                return false;
            }

            ShipLandingUtility.SpawnTransitLoot(record, map, center);

            record.stage = InterstellarTransitStage.None;
            if (record.snapshot != null)
                record.snapshot.currentNodeId = record.destinationId;

            SetCurrentNodeForShip(restoredAnchor, record.destinationId, record.shipDefName, record.shipLabel, record.shipThingId);
            activeTravels.Remove(record);
            VoidMapUtility.DestroyVoidMap(record);

            OrbitalNode destination = GetNodeById(record.destinationId);
            SelectGalaxy(destination?.galaxyId ?? selectedGalaxyId);

            string detail = "Режим: " + ResolveLandingModeLabel(mode) + "\nОписание: " + ShipLandingUtility.DescribeMode(mode) + "\nПоследствия: " + ShipLandingUtility.DescribeModeConsequences(mode);
            AddDiagnostic("Landing", "Посадка завершена", record.shipLabel + " у цели " + ResolveNodeLabel(destination), detail, InterstellarDiagnosticSeverity.Info);
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
            if (destination != null)
            {
                if (destination.id != GetDefaultHomeNodeId())
                    OrbitalNodeMapUtility.ResolveOrCreateMapForNode(destination);

                SelectGalaxy(destination.galaxyId);
            }

            string details = "Рекомендуемый режим: " + ResolveLandingModeLabel(record.preferredLandingMode) + "\n" +
                             "Описание: " + ShipLandingUtility.DescribeMode(record.preferredLandingMode) + "\n" +
                             "Последствия: " + ShipLandingUtility.DescribeModeConsequences(record.preferredLandingMode);

            if (record.intergalacticTravel)
                details += "\nРежим перехода: межгалактический тестовый перелёт (1 день).";

            AddDiagnostic("Transit", "Выход на орбиту", record.shipLabel + " достиг цели " + ResolveNodeLabel(destination), details, InterstellarDiagnosticSeverity.Info);
            Messages.Message("Корабль вышел на орбиту цели: " + ResolveNodeLabel(destination) + ". Рекомендуемый режим посадки: " + ResolveLandingModeLabel(record.preferredLandingMode) + ".", MessageTypeDefOf.PositiveEvent, false);
        }

        private void EnsureLocationRecords()
        {
            shipLocations.RemoveAll(r => r == null);

            for (int i = 0; i < activeTravels.Count; i++)
            {
                ShipTransitRecord travel = activeTravels[i];
                if (travel == null || travel.shipThingId == 0)
                    continue;

                ShipLocationRecord location = FindLocationRecord(travel.shipThingId, travel.shipDefName);
                if (location == null)
                {
                    shipLocations.Add(new ShipLocationRecord
                    {
                        shipThingId = travel.shipThingId,
                        shipDefName = travel.shipDefName,
                        shipLabel = travel.shipLabel,
                        currentNodeId = !string.IsNullOrEmpty(travel.sourceId) ? travel.sourceId : GetDefaultHomeNodeId()
                    });
                }
            }
        }

        private void EnsureHomeworldLocation(Thing ship)
        {
            if (ship == null)
                return;

            SetCurrentNodeForShip(ship, GetDefaultHomeNodeId(), ship.def?.defName, ship.LabelCap);
        }

        private void EnsureCurrentNodeForShip(Thing ship, string nodeId)
        {
            SetCurrentNodeForShip(ship, string.IsNullOrEmpty(nodeId) ? GetDefaultHomeNodeId() : nodeId, ship?.def?.defName, ship?.LabelCap);
        }

        private ShipLocationRecord FindLocationRecord(Thing ship)
        {
            if (ship == null)
                return null;

            return FindLocationRecord(ship.thingIDNumber, ship.def?.defName);
        }

        private ShipLocationRecord FindLocationRecord(int shipThingId, string shipDefName)
        {
            ShipLocationRecord byId = shipLocations.FirstOrDefault(r => r != null && r.shipThingId == shipThingId);
            if (byId != null)
                return byId;

            if (!string.IsNullOrEmpty(shipDefName))
                return shipLocations.FirstOrDefault(r => r != null && r.shipThingId == 0 && r.shipDefName == shipDefName);

            return null;
        }

        private void SetCurrentNodeForShip(Thing ship, string nodeId, string shipDefName = null, string shipLabel = null, int fallbackThingId = 0)
        {
            int thingId = ship != null ? ship.thingIDNumber : fallbackThingId;
            string defName = ship?.def?.defName ?? shipDefName;
            string label = ship != null ? ship.LabelCap : shipLabel;

            ShipLocationRecord location = FindLocationRecord(thingId, defName);
            if (location == null)
            {
                location = new ShipLocationRecord();
                shipLocations.Add(location);
            }

            location.shipThingId = thingId;
            location.shipDefName = defName;
            location.shipLabel = label;
            location.currentNodeId = string.IsNullOrEmpty(nodeId) ? GetDefaultHomeNodeId() : nodeId;
        }

        public void AddDiagnostic(string category, string title, string message, string details, InterstellarDiagnosticSeverity severity)
        {
            if (diagnostics == null)
                diagnostics = new List<InterstellarDiagnosticEntry>();

            diagnostics.Add(new InterstellarDiagnosticEntry
            {
                tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0,
                category = category,
                title = title,
                message = message,
                details = details,
                severity = severity
            });

            if (diagnostics.Count > MaxDiagnostics)
                diagnostics.RemoveRange(0, diagnostics.Count - MaxDiagnostics);
        }

        public IEnumerable<InterstellarDiagnosticEntry> GetDiagnostics(string category = null)
        {
            if (diagnostics == null)
                yield break;

            for (int i = diagnostics.Count - 1; i >= 0; i--)
            {
                InterstellarDiagnosticEntry entry = diagnostics[i];
                if (entry == null)
                    continue;

                if (!string.IsNullOrEmpty(category) && category != "Все" && entry.category != category)
                    continue;

                yield return entry;
            }
        }

        public string BuildDiagnosticsDump()
        {
            StringBuilder sb = new StringBuilder();
            foreach (InterstellarDiagnosticEntry entry in GetDiagnostics())
            {
                sb.AppendLine("[" + entry.severity + "] " + entry.category + " | Tick " + entry.tick);
                sb.AppendLine((entry.title ?? "Запись") + ": " + (entry.message ?? string.Empty));
                if (!string.IsNullOrEmpty(entry.details))
                    sb.AppendLine(entry.details);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public void ClearDiagnostics()
        {
            diagnostics?.Clear();
        }

        private static GalaxyWorldConfiguration CloneConfig(GalaxyWorldConfiguration source)
        {
            if (source == null)
                return null;

            GalaxyWorldConfiguration copy = new GalaxyWorldConfiguration
            {
                selectedGalaxyCount = source.selectedGalaxyCount,
                galaxies = new List<GalaxyDefinition>()
            };

            if (source.galaxies != null)
            {
                foreach (GalaxyDefinition galaxy in source.galaxies)
                {
                    if (galaxy == null)
                        continue;

                    GalaxyDefinition gCopy = new GalaxyDefinition
                    {
                        id = galaxy.id,
                        label = galaxy.label,
                        hasStations = galaxy.hasStations,
                        hasAsteroidBelts = galaxy.hasAsteroidBelts,
                        hasPlanets = galaxy.hasPlanets,
                        stationCount = galaxy.stationCount,
                        beltCount = galaxy.beltCount,
                        planetCount = galaxy.planetCount,
                        solarSystemId = galaxy.solarSystemId,
                        planets = new List<PlanetDefinition_IO>()
                    };

                    if (galaxy.planets != null)
                    {
                        foreach (PlanetDefinition_IO planet in galaxy.planets)
                        {
                            if (planet == null)
                                continue;

                            gCopy.planets.Add(new PlanetDefinition_IO
                            {
                                id = planet.id,
                                label = planet.label,
                                overallRainfall = planet.overallRainfall,
                                overallTemperature = planet.overallTemperature,
                                overallPopulation = planet.overallPopulation,
                                pollution = planet.pollution,
                                coverage = planet.coverage,
                                seedOffset = planet.seedOffset,
                                startPlanet = planet.startPlanet,
                                useVanillaDefaults = planet.useVanillaDefaults,
                                archive = planet.archive == null ? null : new PlanetArchiveData
                                {
                                    seed = planet.archive.seed,
                                    cachedWorldTileCount = planet.archive.cachedWorldTileCount,
                                    generated = planet.archive.generated,
                                    visited = planet.archive.visited,
                                    generatedLabel = planet.archive.generatedLabel,
                                    cachedCoverage = planet.archive.cachedCoverage,
                                    cachedRainfall = planet.archive.cachedRainfall,
                                    cachedTemperature = planet.archive.cachedTemperature,
                                    cachedPopulation = planet.archive.cachedPopulation,
                                    cachedPollution = planet.archive.cachedPollution
                                }
                            });
                        }
                    }

                    copy.galaxies.Add(gCopy);
                }
            }

            return copy;
        }
    }
}
