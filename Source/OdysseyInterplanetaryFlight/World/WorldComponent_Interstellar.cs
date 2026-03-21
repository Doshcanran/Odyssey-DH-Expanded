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
    /// <summary>
    /// Центральный WorldComponent межпланетного мода.
    ///
    /// Отвечает за:
    ///   • хранение орбитальных узлов (планеты / станции / астероиды)
    ///   • ведение активных перелётов
    ///   • отслеживание позиции каждого корабля
    ///   • архивирование поселений на предыдущей планете
    ///     (поселения недоступны, пока корабль на другой планете)
    ///   • применение климатических параметров новой планеты к WorldInfo
    ///   • диагностический лог
    /// </summary>
    public class WorldComponent_Interstellar : WorldComponent
    {
        private const int MaxDiagnostics = 160;

        // ── Основные данные ──────────────────────────────────────────────────
        public List<OrbitalNode> nodes = new List<OrbitalNode>();
        public List<ShipTransitRecord> activeTravels = new List<ShipTransitRecord>();
        public List<ShipLocationRecord> shipLocations = new List<ShipLocationRecord>();
        public List<InterstellarDiagnosticEntry> diagnostics = new List<InterstellarDiagnosticEntry>();
        public bool generated;
        public GalaxyWorldConfiguration galaxyConfig = new GalaxyWorldConfiguration();
        public string selectedGalaxyId = "galaxy_0";
        public string selectedSolarSystemId = "system_0";

        // ── Архив поселений ──────────────────────────────────────────────────
        /// <summary>
        /// ключ = nodeId планеты, значение = список замороженных поселений.
        /// Поселения в архиве удалены с мировой карты и недоступны игроку.
        /// </summary>
        private Dictionary<string, List<SettlementArchiveEntry>> _settlementArchive
            = new Dictionary<string, List<SettlementArchiveEntry>>();

        // ─────────────────────────────────────────────────────────────────────
        public WorldComponent_Interstellar(World world) : base(world) { }

        // ════════════════════════════════════════════════════════════════════
        //  ExposeData
        // ════════════════════════════════════════════════════════════════════
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref generated,              "generated",            false);
            Scribe_Collections.Look(ref nodes,             "nodes",                LookMode.Deep);
            Scribe_Collections.Look(ref activeTravels,     "activeTravels",        LookMode.Deep);
            Scribe_Collections.Look(ref shipLocations,     "shipLocations",        LookMode.Deep);
            Scribe_Collections.Look(ref diagnostics,       "diagnostics",          LookMode.Deep);
            Scribe_Deep.Look(ref galaxyConfig,             "galaxyConfig");
            Scribe_Values.Look(ref selectedGalaxyId,       "selectedGalaxyId",     "galaxy_0");
            Scribe_Values.Look(ref selectedSolarSystemId,  "selectedSolarSystemId","system_0");

            // Архив поселений: Dictionary<string, List<Deep>>
            Scribe_Collections.Look(
                ref _settlementArchive,
                "settlementArchive",
                LookMode.Value,
                LookMode.Deep);

            if (nodes            == null) nodes            = new List<OrbitalNode>();
            if (activeTravels    == null) activeTravels    = new List<ShipTransitRecord>();
            if (shipLocations    == null) shipLocations    = new List<ShipLocationRecord>();
            if (diagnostics      == null) diagnostics      = new List<InterstellarDiagnosticEntry>();
            if (galaxyConfig     == null) galaxyConfig     = GalaxyConfigUtility.CreateDefaultConfiguration();
            if (_settlementArchive == null) _settlementArchive = new Dictionary<string, List<SettlementArchiveEntry>>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                GalaxyConfigUtility.EnsureConsistency(galaxyConfig);
                if (string.IsNullOrEmpty(selectedGalaxyId))
                    selectedGalaxyId = galaxyConfig.galaxies.FirstOrDefault()?.id ?? "galaxy_0";
                EnsureLocationRecords();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  FinalizeInit / Tick
        // ════════════════════════════════════════════════════════════════════
        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            GenerateIfNeeded();
            EnsureLocationRecords();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            if (Find.TickManager == null) return;

            for (int i = activeTravels.Count - 1; i >= 0; i--)
            {
                ShipTransitRecord travel = activeTravels[i];
                if (travel == null || travel.stage != InterstellarTransitStage.InTransit) continue;

                ShipTransitEventUtility.TryProcessEvent(this, travel);

                if (Find.TickManager.TicksGame >= travel.arrivalTick)
                    Arrive(travel);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Генерация узлов
        // ════════════════════════════════════════════════════════════════════
        public void GenerateIfNeeded()
        {
            if (generated && nodes != null && nodes.Count > 0) return;

            if (galaxyConfig == null || galaxyConfig.galaxies == null || galaxyConfig.galaxies.Count == 0)
                galaxyConfig = CloneConfig(InterstellarOdysseyMod.PendingGalaxyConfig)
                               ?? GalaxyConfigUtility.CreateDefaultConfiguration();

            GalaxyConfigUtility.EnsureConsistency(galaxyConfig);
            PlanetArchiveUtility.PrepareArchivesForAllPlanets(galaxyConfig);
            nodes = GalaxyOrbitalNodeFactory.CreateNodes(galaxyConfig);
            if (nodes == null || nodes.Count == 0)
                nodes = DefaultOrbitalNodeFactory.CreateDefaultNodes();

            selectedGalaxyId = ResolveInitialGalaxyId();
            selectedSolarSystemId = GetGalaxyById(selectedGalaxyId)?.solarSystemId ?? "system_0";
            generated = true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Запуск перелёта
        // ════════════════════════════════════════════════════════════════════
        public bool StartTravel(Thing shipAnchor, OrbitalNode destination)
        {
            if (shipAnchor == null || destination == null) return false;

            if (IsShipTravelling(shipAnchor))
            {
                Messages.Message("Корабль уже находится в перелёте.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            string currentNodeId = GetCurrentNodeIdForShip(shipAnchor);
            OrbitalNode current  = GetNodeById(currentNodeId) ?? nodes.FirstOrDefault();

            if (current != null && current.id == destination.id)
            {
                Messages.Message("Корабль уже находится у этой цели.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Диагностика перед стартом
            string launchDiag = InterstellarDiagnostics.BuildLaunchDiagnosticReport(
                this, shipAnchor, current, destination);
            AddDiagnostic("Launch", "Предполётная диагностика", shipAnchor.LabelCap, launchDiag,
                InterstellarDiagnosticSeverity.Info);

            // Валидация корабля
            ShipValidationReport validation = ShipValidationUtility.ValidateForLaunch(shipAnchor);
            if (!validation.CanLaunch)
            {
                AddDiagnostic("Launch", "Старт отклонён", "Валидация не пройдена.",
                    validation.ToUserText(), InterstellarDiagnosticSeverity.Warning);
                Find.WindowStack.Add(new Dialog_MessageBox(validation.ToUserText()));
                return false;
            }

            // Сбор кластера
            if (!ShipCaptureUtility.TryCollectShipCluster(shipAnchor, out ShipClusterData launchCluster))
            {
                AddDiagnostic("Launch", "Старт отклонён", "Не удалось собрать кластер.",
                    launchDiag, InterstellarDiagnosticSeverity.Error);
                Messages.Message("Не удалось определить состав корабля перед стартом.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Оценка тяги / топлива
            ShipPropulsionReport propulsion = ShipPropulsionUtility.Evaluate(launchCluster, current, destination);
            if (!propulsion.hasEnoughThrust)
            {
                AddDiagnostic("Launch", "Недостаточно тяги", propulsion.blockingReason,
                    launchDiag, InterstellarDiagnosticSeverity.Warning);
                Messages.Message("Недостаточная тяга: корабль слишком тяжёлый.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!propulsion.hasEnoughFuel)
            {
                AddDiagnostic("Launch", "Недостаточно топлива", propulsion.blockingReason,
                    launchDiag, InterstellarDiagnosticSeverity.Warning);
                Messages.Message(
                    "Недостаточно топлива. Нужно " + propulsion.fuelNeeded.ToString("0.#")
                    + ", доступно " + propulsion.totalFuel.ToString("0.#") + ".",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Откат при ошибке
            Map   sourceMap       = shipAnchor.Map;
            IntVec3 sourceCell    = shipAnchor.Position;
            ShipSnapshot snapshot = null;
            ShipTransitRecord record = null;
            Dictionary<Thing, float> fuelState = ShipPropulsionUtility.SnapshotFuelState(launchCluster);
            bool fuelCommitted = false;
            bool travelAdded   = false;

            bool intergalactic = current != null
                && !string.Equals(current.galaxyId, destination.galaxyId, StringComparison.Ordinal);

            try
            {
                EnsureCurrentNodeForShip(shipAnchor, currentNodeId);

                // Захватить и деспавнить корабль
                if (!ShipCaptureUtility.TryCaptureAndDespawnShip(shipAnchor, currentNodeId, out snapshot))
                {
                    AddDiagnostic("Launch", "Ошибка захвата корабля", "TryCaptureAndDespawnShip вернул false.",
                        launchDiag, InterstellarDiagnosticSeverity.Error);
                    Messages.Message("Не удалось захватить корабль для перелёта.",
                        MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                // ── АРХИВИРОВАНИЕ ПОСЕЛЕНИЙ ──────────────────────────────────
                // Только при межпланетном прыжке (не к станции / астероиду)
                if (destination.type == OrbitalNodeType.Planet
                    && !string.IsNullOrEmpty(currentNodeId))
                {
                    PlanetArchiveUtility.ArchiveSettlementsForDeparture(
                        currentNodeId, ref _settlementArchive);
                }
                // ────────────────────────────────────────────────────────────

                ShipPropulsionUtility.ConsumeFuel(launchCluster, propulsion.fuelNeeded);
                fuelCommitted = true;

                // Длительность перелёта
                int durationTicks = intergalactic
                    ? GenDate.TicksPerDay
                    : Mathf.Max(2500, Mathf.RoundToInt(
                          Mathf.Max(0.2f, propulsion.travelDays) * GenDate.TicksPerDay));

                record = new ShipTransitRecord
                {
                    shipThingId         = shipAnchor.thingIDNumber,
                    shipLabel           = shipAnchor.LabelCap,
                    shipDefName         = shipAnchor.def.defName,
                    sourceId            = currentNodeId,
                    destinationId       = destination.id,
                    departureTick       = Find.TickManager.TicksGame,
                    arrivalTick         = Find.TickManager.TicksGame + durationTicks,
                    stage               = InterstellarTransitStage.InTransit,
                    snapshot            = snapshot,
                    preferredLandingMode= ShipLandingMode.Precise,
                    intergalacticTravel = intergalactic,
                    sourceGalaxyId      = current?.galaxyId,
                    destinationGalaxyId = destination.galaxyId
                };

                ShipTransitEventUtility.ScheduleNextEvent(record, Find.TickManager.TicksGame);
                activeTravels.Add(record);
                travelAdded = true;

                // Создаём карту вакуума (корабль «живёт» на ней во время перелёта)
                if (!VoidMapUtility.CreateVoidMap(record))
                {
                    AddDiagnostic("Launch", "Предупреждение", "Карта вакуума не создана.",
                        launchDiag, InterstellarDiagnosticSeverity.Warning);
                    Messages.Message("Карта вакуума не создана, но перелёт начат.",
                        MessageTypeDefOf.NeutralEvent, false);
                }

                string msg = intergalactic
                    ? "Начат межгалактический перелёт: " + record.shipLabel
                      + " → " + ResolveNodeLabel(destination) + ". Время: 1 день."
                    : "Начат перелёт: " + record.shipLabel
                      + " → " + ResolveNodeLabel(destination)
                      + ". Топлива потрачено: " + propulsion.fuelNeeded.ToString("0.#");

                AddDiagnostic("Launch", "Перелёт начат", record.shipLabel + " → " + ResolveNodeLabel(destination),
                    launchDiag, InterstellarDiagnosticSeverity.Info);
                Messages.Message(msg, MessageTypeDefOf.PositiveEvent, false);
                return true;
            }
            catch (Exception ex)
            {
                if (travelAdded && record != null) activeTravels.Remove(record);
                if (record != null && record.voidMapTile >= 0)
                    try { VoidMapUtility.DestroyVoidMap(record); } catch { }

                Log.Error("[InterstellarOdyssey] StartTravel rollback: " + ex);
                AddDiagnostic("Launch", "Ошибка старта", ex.Message, ex.ToString(),
                    InterstellarDiagnosticSeverity.Error);

                if (fuelCommitted)
                    try { ShipPropulsionUtility.RestoreFuelState(fuelState); } catch { }

                if (snapshot != null && sourceMap != null)
                    try
                    {
                        snapshot.anchorCell = sourceCell;
                        if (ShipLandingUtility.TryRestoreShip(snapshot, sourceMap, sourceCell,
                                ShipLandingMode.Precise, out Thing restored))
                            SetCurrentNodeForShip(restored, currentNodeId,
                                shipAnchor.def?.defName, shipAnchor.LabelCap, shipAnchor.thingIDNumber);
                    }
                    catch { }

                Messages.Message("Перелёт прерван из-за ошибки. Выполнен откат.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Посадка
        // ════════════════════════════════════════════════════════════════════
        public bool TryLandShip(ShipTransitRecord record, Map map, ShipLandingMode mode)
        {
            if (record == null || map == null || record.snapshot == null) return false;

            if (!ShipLandingUtility.IsModeAllowedForDestination(
                    mode, GetNodeById(record.destinationId), out string reason))
            {
                Messages.Message(reason ?? "Этот режим посадки недоступен.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Захват актуального состояния с карты вакуума
            if (VoidMapUtility.HasVoidMap(record))
            {
                if (!VoidMapUtility.RecaptureShipFromVoidMap(record))
                {
                    AddDiagnostic("Landing", "Ошибка захвата с вакуума",
                        "Не удалось захватить корабль с карты вакуума.",
                        record.shipLabel, InterstellarDiagnosticSeverity.Error);
                    Messages.Message(
                        "Посадка отменена: не удалось захватить состояние корабля с карты вакуума.",
                        MessageTypeDefOf.RejectInput, false);
                    return false;
                }
            }

            if (!ShipLandingUtility.TryFindLandingCenter(record.snapshot, map, mode, out IntVec3 center))
                center = map.Center;

            if (!ShipLandingUtility.TryRestoreShip(record.snapshot, map, center, mode, out Thing restoredAnchor))
            {
                AddDiagnostic("Landing", "Посадка не удалась", "TryRestoreShip вернул false.",
                    "Map=" + (map.Parent?.LabelCap ?? map.ToString()) + " mode=" + mode,
                    InterstellarDiagnosticSeverity.Error);
                return false;
            }

            ShipLandingUtility.SpawnTransitLoot(record, map, center);

            // ── ВОССТАНОВЛЕНИЕ ПОСЕЛЕНИЙ ─────────────────────────────────────
            OrbitalNode destNode = GetNodeById(record.destinationId);
            if (destNode != null && destNode.type == OrbitalNodeType.Planet)
            {
                // Применить параметры планеты назначения к WorldInfo
                PlanetDefinition_IO planetDef = FindPlanetDefById(record.destinationId);
                if (planetDef != null)
                    PlanetArchiveUtility.ApplyPlanetParamsToWorldInfo(planetDef);

                // Восстановить поселения на новой планете
                PlanetArchiveUtility.RestoreSettlementsOnArrival(
                    record.destinationId, ref _settlementArchive);
            }
            // ─────────────────────────────────────────────────────────────────

            record.stage = InterstellarTransitStage.None;
            if (record.snapshot != null)
                record.snapshot.currentNodeId = record.destinationId;

            SetCurrentNodeForShip(restoredAnchor, record.destinationId,
                record.shipDefName, record.shipLabel, record.shipThingId);
            activeTravels.Remove(record);
            VoidMapUtility.DestroyVoidMap(record);

            SelectGalaxy(destNode?.galaxyId ?? selectedGalaxyId);

            string detail = "Режим: " + ResolveLandingModeLabel(mode)
                + "\n" + ShipLandingUtility.DescribeMode(mode)
                + "\n" + ShipLandingUtility.DescribeModeConsequences(mode);

            AddDiagnostic("Landing", "Посадка завершена",
                record.shipLabel + " у цели " + ResolveNodeLabel(destNode),
                detail, InterstellarDiagnosticSeverity.Info);

            Messages.Message(
                "Корабль " + (record.shipLabel ?? "?") + " совершил посадку у "
                + ResolveNodeLabel(destNode) + " [" + ResolveLandingModeLabel(mode) + "].",
                MessageTypeDefOf.PositiveEvent, false);

            // Переключаем камеру на целевую карту
            if (Current.ProgramState == ProgramState.Playing)
                Current.Game.CurrentMap = map;

            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Прибытие (конец перелёта, ожидание посадки)
        // ════════════════════════════════════════════════════════════════════
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

            string details = "Рекомендуемый режим: " + ResolveLandingModeLabel(record.preferredLandingMode)
                + "\n" + ShipLandingUtility.DescribeMode(record.preferredLandingMode)
                + "\n" + ShipLandingUtility.DescribeModeConsequences(record.preferredLandingMode);

            AddDiagnostic("Transit", "Выход на орбиту",
                record.shipLabel + " прибыл к " + ResolveNodeLabel(destination),
                details, InterstellarDiagnosticSeverity.Info);

            Messages.Message(
                "Корабль вышел на орбиту: " + ResolveNodeLabel(destination)
                + ". Рекомендуемый режим посадки: "
                + ResolveLandingModeLabel(record.preferredLandingMode) + ".",
                MessageTypeDefOf.PositiveEvent, false);

            // Открываем окно посадки
            if (Current.ProgramState == ProgramState.Playing)
                Find.WindowStack.Add(new Window_ShipLanding(record));
        }

        // ════════════════════════════════════════════════════════════════════
        //  Вспомогательные геттеры / сеттеры
        // ════════════════════════════════════════════════════════════════════
        public GalaxyDefinition GetGalaxyById(string galaxyId)
            => galaxyConfig?.galaxies?.FirstOrDefault(g => g != null && g.id == galaxyId);

        public IEnumerable<GalaxyDefinition> GetGalaxies()
            => galaxyConfig?.galaxies ?? Enumerable.Empty<GalaxyDefinition>();

        public IEnumerable<OrbitalNode> GetNodesForGalaxy(string galaxyId)
        {
            GenerateIfNeeded();
            if (string.IsNullOrEmpty(galaxyId)) galaxyId = selectedGalaxyId;
            return nodes.Where(n => n != null && n.galaxyId == galaxyId);
        }

        public IEnumerable<OrbitalNode> GetNodesForSelectedGalaxy()
            => GetNodesForGalaxy(selectedGalaxyId);

        public void SelectGalaxy(string galaxyId)
        {
            if (string.IsNullOrEmpty(galaxyId) || GetGalaxyById(galaxyId) == null) return;
            selectedGalaxyId = galaxyId;
            selectedSolarSystemId = GetGalaxyById(galaxyId)?.solarSystemId ?? selectedSolarSystemId;
        }

        public string GetDefaultHomeNodeId()
        {
            GenerateIfNeeded();
            return GalaxyConfigUtility.GetStartPlanet(galaxyConfig)?.id ?? "homeworld";
        }

        public string ResolveInitialGalaxyId()
        {
            PlanetDefinition_IO startPlanet = GalaxyConfigUtility.GetStartPlanet(galaxyConfig);
            OrbitalNode node = nodes.FirstOrDefault(n => n != null && n.id == startPlanet?.id);
            return node?.galaxyId ?? galaxyConfig.galaxies.FirstOrDefault()?.id ?? "galaxy_0";
        }

        public bool IsShipTravelling(Thing ship)
            => ship != null && activeTravels.Any(
                t => t != null && t.shipThingId == ship.thingIDNumber
                  && t.stage != InterstellarTransitStage.None);

        public OrbitalNode GetCurrentNodeForShip(Thing ship)
        {
            string nodeId = GetCurrentNodeIdForShip(ship);
            return GetNodeById(nodeId) ?? GetNodeById(GetDefaultHomeNodeId()) ?? nodes.FirstOrDefault();
        }

        public string GetCurrentNodeIdForShip(Thing ship)
        {
            if (ship == null) return GetDefaultHomeNodeId();

            ShipTransitRecord record = activeTravels.FirstOrDefault(
                t => t != null && t.shipThingId == ship.thingIDNumber);
            if (record != null)
                return record.stage == InterstellarTransitStage.AwaitingLanding
                    ? (record.destinationId ?? record.sourceId)
                    : (record.sourceId ?? GetDefaultHomeNodeId());

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
            if (node == null) return "Неизвестно";
            return string.IsNullOrEmpty(node.label) ? node.id : node.label;
        }

        public string ResolveLandingModeLabel(ShipLandingMode mode)
        {
            switch (mode)
            {
                case ShipLandingMode.Emergency:         return "аварийная";
                case ShipLandingMode.OrbitalDrop:       return "орбитальный дроп";
                case ShipLandingMode.UnpreparedSurface: return "неподготовленная поверхность";
                case ShipLandingMode.StationDocking:    return "стыковка";
                default: return "точная";
            }
        }

        public IEnumerable<ShipTransitRecord> GetLandingReadyTravels()
            => activeTravels.Where(t => t != null && t.stage == InterstellarTransitStage.AwaitingLanding);

        // ─────────────────────────────────────────────────────────────────────
        //  Местоположение кораблей
        // ─────────────────────────────────────────────────────────────────────
        private void EnsureLocationRecords()
        {
            shipLocations.RemoveAll(r => r == null);
            foreach (ShipTransitRecord travel in activeTravels)
            {
                if (travel == null || travel.shipThingId == 0) continue;
                ShipLocationRecord loc = FindLocationRecord(travel.shipThingId, travel.shipDefName);
                if (loc == null)
                    shipLocations.Add(new ShipLocationRecord
                    {
                        shipThingId  = travel.shipThingId,
                        shipDefName  = travel.shipDefName,
                        shipLabel    = travel.shipLabel,
                        currentNodeId = travel.sourceId ?? GetDefaultHomeNodeId()
                    });
            }
        }

        private void EnsureHomeworldLocation(Thing ship)
        {
            if (ship == null) return;
            SetCurrentNodeForShip(ship, GetDefaultHomeNodeId(), ship.def?.defName, ship.LabelCap);
        }

        private void EnsureCurrentNodeForShip(Thing ship, string nodeId)
            => SetCurrentNodeForShip(ship,
                string.IsNullOrEmpty(nodeId) ? GetDefaultHomeNodeId() : nodeId,
                ship?.def?.defName, ship?.LabelCap);

        private ShipLocationRecord FindLocationRecord(Thing ship)
            => ship == null ? null : FindLocationRecord(ship.thingIDNumber, ship.def?.defName);

        private ShipLocationRecord FindLocationRecord(int id, string defName)
        {
            ShipLocationRecord byId = shipLocations.FirstOrDefault(r => r != null && r.shipThingId == id);
            if (byId != null) return byId;
            if (!string.IsNullOrEmpty(defName))
                return shipLocations.FirstOrDefault(r => r != null && r.shipThingId == 0 && r.shipDefName == defName);
            return null;
        }

        private void SetCurrentNodeForShip(Thing ship, string nodeId,
            string shipDefName = null, string shipLabel = null, int fallbackId = 0)
        {
            int thingId = ship != null ? ship.thingIDNumber : fallbackId;
            string defName = ship?.def?.defName ?? shipDefName;
            string label   = ship != null ? ship.LabelCap : shipLabel;

            ShipLocationRecord loc = FindLocationRecord(thingId, defName);
            if (loc == null) { loc = new ShipLocationRecord(); shipLocations.Add(loc); }

            loc.shipThingId   = thingId;
            loc.shipDefName   = defName;
            loc.shipLabel     = label;
            loc.currentNodeId = string.IsNullOrEmpty(nodeId) ? GetDefaultHomeNodeId() : nodeId;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Утилиты
        // ─────────────────────────────────────────────────────────────────────
        private PlanetDefinition_IO FindPlanetDefById(string planetNodeId)
        {
            if (string.IsNullOrEmpty(planetNodeId) || galaxyConfig?.galaxies == null) return null;
            foreach (GalaxyDefinition g in galaxyConfig.galaxies)
                if (g?.planets != null)
                    foreach (PlanetDefinition_IO p in g.planets)
                        if (p != null && p.id == planetNodeId)
                            return p;
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Диагностика
        // ─────────────────────────────────────────────────────────────────────
        public void AddDiagnostic(string category, string title, string message,
            string details, InterstellarDiagnosticSeverity severity)
        {
            if (diagnostics == null) diagnostics = new List<InterstellarDiagnosticEntry>();
            diagnostics.Add(new InterstellarDiagnosticEntry
            {
                tick     = Find.TickManager?.TicksGame ?? 0,
                category = category,
                title    = title,
                message  = message,
                details  = details,
                severity = severity
            });
            if (diagnostics.Count > MaxDiagnostics)
                diagnostics.RemoveRange(0, diagnostics.Count - MaxDiagnostics);
        }

        public IEnumerable<InterstellarDiagnosticEntry> GetDiagnostics(string category = null)
        {
            if (diagnostics == null) yield break;
            for (int i = diagnostics.Count - 1; i >= 0; i--)
            {
                InterstellarDiagnosticEntry entry = diagnostics[i];
                if (entry == null) continue;
                if (!string.IsNullOrEmpty(category) && category != "Все" && entry.category != category) continue;
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
                if (!string.IsNullOrEmpty(entry.details)) sb.AppendLine(entry.details);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public void ClearDiagnostics() => diagnostics?.Clear();

        // ─────────────────────────────────────────────────────────────────────
        //  Клонирование конфига
        // ─────────────────────────────────────────────────────────────────────
        private static GalaxyWorldConfiguration CloneConfig(GalaxyWorldConfiguration source)
        {
            if (source == null) return null;
            GalaxyWorldConfiguration copy = new GalaxyWorldConfiguration
            {
                selectedGalaxyCount = source.selectedGalaxyCount,
                galaxies = new List<GalaxyDefinition>()
            };
            if (source.galaxies == null) return copy;

            foreach (GalaxyDefinition galaxy in source.galaxies)
            {
                if (galaxy == null) continue;
                GalaxyDefinition gCopy = new GalaxyDefinition
                {
                    id = galaxy.id, label = galaxy.label, solarSystemId = galaxy.solarSystemId,
                    hasStations = galaxy.hasStations, hasAsteroidBelts = galaxy.hasAsteroidBelts,
                    hasPlanets = galaxy.hasPlanets, stationCount = galaxy.stationCount,
                    beltCount = galaxy.beltCount, planetCount = galaxy.planetCount,
                    planets = new List<PlanetDefinition_IO>()
                };
                if (galaxy.planets != null)
                    foreach (PlanetDefinition_IO p in galaxy.planets)
                    {
                        if (p == null) continue;
                        gCopy.planets.Add(new PlanetDefinition_IO
                        {
                            id = p.id, label = p.label,
                            overallRainfall = p.overallRainfall, overallTemperature = p.overallTemperature,
                            overallPopulation = p.overallPopulation, pollution = p.pollution,
                            coverage = p.coverage, seedOffset = p.seedOffset,
                            startPlanet = p.startPlanet, useVanillaDefaults = p.useVanillaDefaults,
                            archive = p.archive == null ? null : new PlanetArchiveData
                            {
                                seed = p.archive.seed, generated = p.archive.generated,
                                visited = p.archive.visited, generatedLabel = p.archive.generatedLabel,
                                cachedCoverage = p.archive.cachedCoverage,
                                cachedRainfall = p.archive.cachedRainfall,
                                cachedTemperature = p.archive.cachedTemperature,
                                cachedPopulation = p.archive.cachedPopulation,
                                cachedPollution = p.archive.cachedPollution,
                                cachedWorldTileCount = p.archive.cachedWorldTileCount
                            }
                        });
                    }
                copy.galaxies.Add(gCopy);
            }
            return copy;
        }
    }
}
