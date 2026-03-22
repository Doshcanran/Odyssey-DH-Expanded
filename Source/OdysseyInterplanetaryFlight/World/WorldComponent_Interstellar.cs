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

        // ── Текущая планета ──────────────────────────────────────────────────
        public string currentPlanetNodeId = "homeworld";

        // ── Архив состояний планет (seed + поселения) ────────────────────────
        private Dictionary<string, PlanetSwitcher.PlanetState> _planetStateArchive
            = new Dictionary<string, PlanetSwitcher.PlanetState>();

        // ── Архив карт планет (ссылки на файлы) ─────────────────────────────
        /// <summary>ключ = nodeId, значение = список метаданных сохранённых карт</summary>
        private Dictionary<string, List<ArchivedMapMeta>> _mapArchive
            = new Dictionary<string, List<ArchivedMapMeta>>();

        public WorldComponent_Interstellar(World world) : base(world) { }

        // ════════════════════════════════════════════════════════════════════
        //  ExposeData
        // ════════════════════════════════════════════════════════════════════
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref generated,             "generated",             false);
            Scribe_Collections.Look(ref nodes,            "nodes",                 LookMode.Deep);
            Scribe_Collections.Look(ref activeTravels,    "activeTravels",         LookMode.Deep);
            Scribe_Collections.Look(ref shipLocations,    "shipLocations",         LookMode.Deep);
            Scribe_Collections.Look(ref diagnostics,      "diagnostics",           LookMode.Deep);
            Scribe_Deep.Look(ref galaxyConfig,            "galaxyConfig");
            Scribe_Values.Look(ref selectedGalaxyId,      "selectedGalaxyId",      "galaxy_0");
            Scribe_Values.Look(ref selectedSolarSystemId, "selectedSolarSystemId", "system_0");
            Scribe_Values.Look(ref currentPlanetNodeId,   "currentPlanetNodeId",   "homeworld");

            Scribe_Collections.Look(ref _planetStateArchive, "planetStateArchive",
                LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref _mapArchive, "mapArchive",
                LookMode.Value, LookMode.Deep);

            if (nodes               == null) nodes               = new List<OrbitalNode>();
            if (activeTravels       == null) activeTravels       = new List<ShipTransitRecord>();
            if (shipLocations       == null) shipLocations       = new List<ShipLocationRecord>();
            if (diagnostics         == null) diagnostics         = new List<InterstellarDiagnosticEntry>();
            if (galaxyConfig        == null) galaxyConfig        = GalaxyConfigUtility.CreateDefaultConfiguration();
            if (_planetStateArchive == null) _planetStateArchive = new Dictionary<string, PlanetSwitcher.PlanetState>();
            if (_mapArchive         == null) _mapArchive         = new Dictionary<string, List<ArchivedMapMeta>>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                GalaxyConfigUtility.EnsureConsistency(galaxyConfig);
                if (string.IsNullOrEmpty(selectedGalaxyId))
                    selectedGalaxyId = galaxyConfig.galaxies.FirstOrDefault()?.id ?? "galaxy_0";
                EnsureLocationRecords();
            }
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

            string launchDiag = InterstellarDiagnostics.BuildLaunchDiagnosticReport(
                this, shipAnchor, current, destination);
            AddDiagnostic("Launch", "Предполётная диагностика", shipAnchor.LabelCap,
                launchDiag, InterstellarDiagnosticSeverity.Info);

            ShipValidationReport validation = ShipValidationUtility.ValidateForLaunch(shipAnchor);
            if (!validation.CanLaunch)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(validation.ToUserText()));
                return false;
            }

            if (!ShipCaptureUtility.TryCollectShipCluster(shipAnchor, out ShipClusterData launchCluster))
            {
                Messages.Message("Не удалось определить состав корабля.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            ShipPropulsionReport propulsion = ShipPropulsionUtility.Evaluate(launchCluster, current, destination);
            if (!propulsion.hasEnoughThrust)
            {
                Messages.Message("Недостаточная тяга.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (!propulsion.hasEnoughFuel)
            {
                Messages.Message("Недостаточно топлива. Нужно " + propulsion.fuelNeeded.ToString("0.#")
                    + ", доступно " + propulsion.totalFuel.ToString("0.#") + ".",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            Map    sourceMap   = shipAnchor.Map;
            IntVec3 srcCell    = shipAnchor.Position;
            ShipSnapshot snapshot = null;
            ShipTransitRecord record = null;
            var fuelState = ShipPropulsionUtility.SnapshotFuelState(launchCluster);
            bool fuelCommitted = false, travelAdded = false;
            bool intergalactic = current != null
                && !string.Equals(current.galaxyId, destination.galaxyId, StringComparison.Ordinal);
            bool isPlanetJump  = destination.type == OrbitalNodeType.Planet
                && !string.IsNullOrEmpty(currentNodeId);

            try
            {
                EnsureCurrentNodeForShip(shipAnchor, currentNodeId);

                if (!ShipCaptureUtility.TryCaptureAndDespawnShip(shipAnchor, currentNodeId, out snapshot))
                {
                    Messages.Message("Не удалось захватить корабль.", MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                // ── При межпланетном прыжке: сохраняем состояние и архивируем карты ПОСЛЕ захвата корабля ──
                if (isPlanetJump)
                {
                    PlanetDefinition_IO srcDef = FindPlanetDefById(currentNodeId);
                    _planetStateArchive[currentNodeId] =
                        PlanetSwitcher.CapturePlanetState(currentNodeId, srcDef);

                    List<ArchivedMapMeta> metas = MapArchiver.ArchiveAllMaps(currentNodeId);
                    _mapArchive[currentNodeId] = metas;

                    Messages.Message(
                        "Карты планеты «" + (srcDef?.label ?? currentNodeId)
                        + "» сохранены (" + metas.Count + " шт.).",
                        MessageTypeDefOf.NeutralEvent, false);
                }

                ShipPropulsionUtility.ConsumeFuel(launchCluster, propulsion.fuelNeeded);
                fuelCommitted = true;

                // TODO ВРЕМЕННО: все перелёты = 1 день для тестирования посадки.
                // Восстановить после отладки:
                // int durationTicks = intergalactic
                //     ? GenDate.TicksPerDay
                //     : Mathf.Max(2500, Mathf.RoundToInt(
                //           Mathf.Max(0.2f, propulsion.travelDays) * GenDate.TicksPerDay));
                int durationTicks = GenDate.TicksPerDay;

                record = new ShipTransitRecord
                {
                    shipThingId          = shipAnchor.thingIDNumber,
                    shipLabel            = shipAnchor.LabelCap,
                    shipDefName          = shipAnchor.def.defName,
                    sourceId             = currentNodeId,
                    destinationId        = destination.id,
                    departureTick        = Find.TickManager.TicksGame,
                    arrivalTick          = Find.TickManager.TicksGame + durationTicks,
                    stage                = InterstellarTransitStage.InTransit,
                    snapshot             = snapshot,
                    preferredLandingMode = ShipLandingMode.Precise,
                    intergalacticTravel  = intergalactic,
                    sourceGalaxyId       = current?.galaxyId,
                    destinationGalaxyId  = destination.galaxyId
                };

                ShipTransitEventUtility.ScheduleNextEvent(record, Find.TickManager.TicksGame);
                activeTravels.Add(record);
                travelAdded = true;

                if (!VoidMapUtility.CreateVoidMap(record))
                    Messages.Message("Карта вакуума не создана, перелёт продолжается.",
                        MessageTypeDefOf.NeutralEvent, false);

                string msg = intergalactic
                    ? "Межгалактический перелёт: " + record.shipLabel + " → " + ResolveNodeLabel(destination) + ". Время: 1 день."
                    : "Перелёт: " + record.shipLabel + " → " + ResolveNodeLabel(destination)
                      + ". Топлива: " + propulsion.fuelNeeded.ToString("0.#");

                AddDiagnostic("Launch", "Перелёт начат", msg, launchDiag, InterstellarDiagnosticSeverity.Info);
                Messages.Message(msg, MessageTypeDefOf.PositiveEvent, false);
                return true;
            }
            catch (Exception ex)
            {
                if (travelAdded && record != null) activeTravels.Remove(record);
                if (record?.voidMapTile >= 0) try { VoidMapUtility.DestroyVoidMap(record); } catch { }
                Log.Error("[IO] StartTravel rollback: " + ex);
                if (fuelCommitted) try { ShipPropulsionUtility.RestoreFuelState(fuelState); } catch { }
                if (snapshot != null && sourceMap != null)
                    try { ShipLandingUtility.TryRestoreShip(snapshot, sourceMap, srcCell,
                        ShipLandingMode.Precise, out _); } catch { }
                Messages.Message("Перелёт прерван. Откат выполнен.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Посадка — с полной сменой планеты и восстановлением карт
        // ════════════════════════════════════════════════════════════════════
        public bool TryLandShip(ShipTransitRecord record, Map map, ShipLandingMode mode)
        {
            if (record == null || record.snapshot == null) return false;
            if (map == null)
            {
                Messages.Message("Целевая карта не указана.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            mode = ShipLandingMode.Precise;

            OrbitalNode destNode = GetNodeById(record.destinationId);

            if (VoidMapUtility.HasVoidMap(record))
            {
                if (!VoidMapUtility.RecaptureShipFromVoidMap(record))
                {
                    Messages.Message("Посадка отменена: не удалось захватить корабль с карты вакуума.",
                        MessageTypeDefOf.RejectInput, false);
                    return false;
                }
                VoidMapUtility.DestroyVoidMap(record);
            }

            if (destNode != null && destNode.type == OrbitalNodeType.Planet)
                currentPlanetNodeId = destNode.id;

            Map landingMap = map;

            if (!ShipLandingUtility.TryFindLandingCenter(record.snapshot, landingMap, mode, out IntVec3 center))
                center = landingMap.Center;

            if (!ShipLandingUtility.TryRestoreShip(record.snapshot, landingMap, center, mode, out Thing restoredAnchor))
            {
                AddDiagnostic("Landing", "Посадка не удалась", "TryRestoreShip вернул false.",
                    "mode=Precise", InterstellarDiagnosticSeverity.Error);
                return false;
            }

            ShipLandingUtility.SpawnTransitLoot(record, landingMap, center);

            record.stage = InterstellarTransitStage.None;
            if (record.snapshot != null)
                record.snapshot.currentNodeId = record.destinationId;

            currentPlanetNodeId = record.destinationId;

            SetCurrentNodeForShip(restoredAnchor, record.destinationId,
                record.shipDefName, record.shipLabel, record.shipThingId);
            activeTravels.Remove(record);
            SelectGalaxy(destNode?.galaxyId ?? selectedGalaxyId);

            AddDiagnostic("Landing", "Посадка завершена",
                record.shipLabel + " у " + ResolveNodeLabel(destNode),
                "Режим: стандартная посадка", InterstellarDiagnosticSeverity.Info);

            Messages.Message(
                "Корабль " + (record.shipLabel ?? "?") + " совершил посадку у "
                + ResolveNodeLabel(destNode) + ".",
                MessageTypeDefOf.PositiveEvent, false);

            if (Current.ProgramState == ProgramState.Playing)
                Current.Game.CurrentMap = landingMap;

            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Смена мирового шара ДО выбора тайла
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Шаг 1: вызывается из Window_ShipLanding при нажатии "Выбрать тайл".
        /// Перегенерирует мировой шар с seed новой планеты.
        /// После этого игрок выбирает тайл на НОВОМ мире.
        /// Coverage не меняется — TilesCount одинаковый.
        /// </summary>
        public bool PrepareNewPlanetWorld(ShipTransitRecord record)
        {
            if (record == null) return false;

            OrbitalNode destNode = GetNodeById(record.destinationId);
            PlanetDefinition_IO destDef = FindPlanetDefById(record.destinationId);

            // Захватываем корабль с карты вакуума ДО перегенерации мира
            if (VoidMapUtility.HasVoidMap(record))
            {
                if (!VoidMapUtility.RecaptureShipFromVoidMap(record))
                {
                    Messages.Message("Не удалось захватить корабль с карты вакуума.",
                        MessageTypeDefOf.RejectInput, false);
                    return false;
                }
                VoidMapUtility.DestroyVoidMap(record);
            }

            // Обновляем параметры мира для новой планеты (только WorldInfo.seedString и климат)
            // НЕ трогаем FactionManager — иначе пешки теряют принадлежность к фракции игрока
            if (destDef != null)
            {
                _planetStateArchive.TryGetValue(record.destinationId,
                    out PlanetSwitcher.PlanetState existingState);

                bool switched = PlanetSwitcher.SwitchToPlanet(
                    destDef, record.destinationId, existingState, out int _);
                if (!switched)
                    return false;

                // ReassignToPlayerFaction больше не нужна:
                // SwitchToPlanet теперь НЕ вызывает GenerateWorld,
                // поэтому FactionManager и faction-ссылки остаются нетронутыми.
            }

            currentPlanetNodeId = record.destinationId;

            AddDiagnostic("Landing", "Мир перегенерирован",
                "Новая планета: " + ResolveNodeLabel(destNode),
                "seed=" + (destDef?.id ?? "?"), InterstellarDiagnosticSeverity.Info);

            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Посадка на новую планету — смена мира + генерация карты на тайле
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается из Window_ShipLanding после того как игрок выбрал тайл.
        /// 1. WorldGenerator.GenerateWorld с seed новой планеты (coverage та же → tile валиден)
        /// 2. GenerateMapAtTile → карта в новом мире
        /// 3. TryRestoreShip → корабль на карте
        /// </summary>
        public bool TryLandShipOnNewPlanet(ShipTransitRecord record, int tile, ShipLandingMode mode)
        {
            if (record == null || record.snapshot == null) return false;

            mode = ShipLandingMode.Precise;

            OrbitalNode destNode = GetNodeById(record.destinationId);

            Map landingMap = InterplanetaryLandingHelper.GenerateMapAtTile(tile, destNode);
            if (landingMap == null)
            {
                Messages.Message("Не удалось создать карту на выбранном тайле.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!ShipLandingUtility.TryFindLandingCenter(record.snapshot, landingMap, mode, out IntVec3 center))
                center = landingMap.Center;

            if (!ShipLandingUtility.TryRestoreShip(record.snapshot, landingMap, center, mode, out Thing restoredAnchor))
            {
                AddDiagnostic("Landing", "Посадка не удалась", "TryRestoreShip вернул false.",
                    "tile=" + tile, InterstellarDiagnosticSeverity.Error);
                return false;
            }

            ShipLandingUtility.SpawnTransitLoot(record, landingMap, center);

            record.stage = InterstellarTransitStage.None;
            if (record.snapshot != null)
                record.snapshot.currentNodeId = record.destinationId;

            currentPlanetNodeId = record.destinationId;

            SetCurrentNodeForShip(restoredAnchor, record.destinationId,
                record.shipDefName, record.shipLabel, record.shipThingId);
            activeTravels.Remove(record);
            SelectGalaxy(destNode?.galaxyId ?? selectedGalaxyId);

            Messages.Message(
                "Корабль " + (record.shipLabel ?? "?") + " совершил посадку на «"
                + ResolveNodeLabel(destNode) + "».",
                MessageTypeDefOf.PositiveEvent, false);

            if (Current.ProgramState == ProgramState.Playing)
                Current.Game.CurrentMap = landingMap;

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Создание карты на новом тайле
        // ─────────────────────────────────────────────────────────────────────
        private static Map CreateLandingMap(int tile, OrbitalNode node)
        {
            if (tile < 0 || tile >= Find.WorldGrid.TilesCount)
            {
                Log.Warning("[IO] CreateLandingMap: некорректный тайл " + tile);
                return null;
            }

            int finalTile = tile;
            if (Find.WorldObjects.AnyWorldObjectAt(finalTile))
            {
                // Ищем свободный тайл — тот же подход что OrbitalNodeMapUtility (без Tile.biome)
                int count = Find.WorldGrid.TilesCount;
                for (int i = 1; i < count; i++)
                {
                    int candidate = (tile + i * 37) % count;
                    if (!Find.WorldObjects.AnyWorldObjectAt(candidate))
                    { finalTile = candidate; break; }
                }
            }

            // WorldObjectMaker.MakeWorldObject(Settlement) возвращает Settlement (наследник MapParent)
            Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            settlement.Tile = finalTile;
            settlement.Name = node?.label ?? "Посадочная зона";

            // Безопасно получаем фракцию — после GenerateWorld может быть null
            Faction playerFaction = SafeGetPlayerFaction();
            if (playerFaction != null)
                settlement.SetFaction(playerFaction);

            Find.WorldObjects.Add(settlement);
            MapParent parent = settlement;

            MapGeneratorDef genDef =
                DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Player") ??
                DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Faction") ??
                DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();

            return MapGenerator.GenerateMap(new IntVec3(250, 1, 250), parent, genDef);
        }

        /// <summary>
        /// Исправляет faction всех заспавненных объектов корабля на карте.
        /// После WorldGenerator.GenerateWorld старый объект Faction.OfPlayer заменяется новым,
        /// поэтому уже заспавненные things/pawns имеют ссылку на старую (недействительную) фракцию.
        /// Вызывается ПОСЛЕ GenSpawn.Spawn — только на уже размещённых объектах.
        /// </summary>
        private static void FixSpawnedFactions(Map map)
        {
            if (map == null) return;

            Faction newPlayer = SafeGetPlayerFaction();
            if (newPlayer == null)
            {
                Log.Warning("[IO] FixSpawnedFactions: фракция игрока не найдена.");
                return;
            }

            int fixedThings = 0;
            int fixedPawns  = 0;

            // Здания — SetFactionDirect чтобы не триггерить лишние события
            foreach (Thing thing in map.listerThings.AllThings.ToList())
            {
                if (thing == null || thing.Destroyed || !thing.Spawned) continue;
                if (thing.def?.category != ThingCategory.Building) continue;
                if (thing.Faction == newPlayer) continue;

                // Проверяем что это была наша постройка (была в faction игрока)
                // Если faction == null или faction.IsPlayer (старый) — исправляем
                if (thing.Faction == null || thing.Faction.IsPlayer)
                {
                    thing.SetFactionDirect(newPlayer);
                    fixedThings++;
                }
            }

            // Пешки — SetFaction с полным обновлением
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == null || pawn.Dead) continue;
                if (pawn.Faction == newPlayer) continue;

                if (pawn.Faction == null || pawn.Faction.IsPlayer)
                {
                    try
                    {
                        pawn.SetFaction(newPlayer);
                        fixedPawns++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[IO] FixSpawnedFactions pawn " + pawn.LabelShortCap + ": " + ex.Message);
                    }
                }
            }

            Log.Message("[IO] FixSpawnedFactions: исправлено " + fixedThings
                + " зданий, " + fixedPawns + " пешек на карте " + map);
        }



/// <summary>
/// После посадки колонисты иногда оказываются в гостевом состоянии и перестают
/// управляться игроком. Здесь принудительно нормализуем их faction и guest-state.
/// </summary>
private static void NormalizeLandedPawns(Map map)
{
    if (map == null) return;

    Faction playerFaction = SafeGetPlayerFaction();
    if (playerFaction == null)
    {
        Log.Warning("[IO] NormalizeLandedPawns: player faction not found.");
        return;
    }

    int normalized = 0;

    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
    {
        if (pawn == null || pawn.Dead)
            continue;

        bool shouldNormalize =
            pawn.Faction == null ||
            pawn.Faction == playerFaction ||
            pawn.Faction.IsPlayer ||
            pawn.IsColonist ||
            pawn.IsPrisonerOfColony ||
            pawn.IsSlaveOfColony;

        if (!shouldNormalize)
            continue;

        try
        {
            if (pawn.Faction != playerFaction)
                pawn.SetFaction(playerFaction);
        }
        catch (Exception ex)
        {
            Log.Warning("[IO] NormalizeLandedPawns SetFaction " + pawn.LabelShortCap + ": " + ex.Message);
        }

        try
        {
            if (pawn.guest != null)
            {
                foreach (var field in pawn.guest.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                {
                    string lower = field.Name.ToLowerInvariant();

                    if (lower.Contains("hostfaction") && typeof(Faction).IsAssignableFrom(field.FieldType))
                    {
                        field.SetValue(pawn.guest, null);
                        continue;
                    }

                    if ((lower.Contains("gueststatus") || lower == "status") && field.FieldType.IsEnum)
                    {
                        field.SetValue(pawn.guest, Enum.ToObject(field.FieldType, 0));
                        continue;
                    }

                    if ((lower.Contains("gueststatus") || lower == "status") && field.FieldType == typeof(int))
                    {
                        field.SetValue(pawn.guest, 0);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[IO] NormalizeLandedPawns guest reset " + pawn.LabelShortCap + ": " + ex.Message);
        }

        try { pawn.jobs?.StopAll(); } catch { }
        try { pawn.mindState?.Notify_TuckedIntoBed(); } catch { }

        normalized++;
    }

    Log.Message("[IO] NormalizeLandedPawns: нормализовано пешек " + normalized + " на карте " + map);
}

        /// <summary>
        /// Безопасно возвращает фракцию игрока без исключений.
        /// После WorldGenerator.GenerateWorld FactionManager может быть ещё не инициализирован.
        /// </summary>
        private static Faction SafeGetPlayerFaction()
        {
            try { return Find.FactionManager?.OfPlayer; }
            catch
            {
                try { return Find.FactionManager?.AllFactions?.FirstOrDefault(f => f != null && f.IsPlayer); }
                catch { return null; }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Прибытие
        // ════════════════════════════════════════════════════════════════════
        private void Arrive(ShipTransitRecord record)
        {
            record.stage = InterstellarTransitStage.AwaitingLanding;
            if (record.snapshot != null)
                record.snapshot.currentNodeId = record.destinationId;

            OrbitalNode destination = GetNodeById(record.destinationId);
            if (destination != null)
            {
                if (destination.type != OrbitalNodeType.Planet
                    && destination.id != GetDefaultHomeNodeId())
                    OrbitalNodeMapUtility.ResolveOrCreateMapForNode(destination);
                SelectGalaxy(destination.galaxyId);
            }

            AddDiagnostic("Transit", "Выход на орбиту",
                record.shipLabel + " → " + ResolveNodeLabel(destination),
                "Режим: " + ResolveLandingModeLabel(record.preferredLandingMode),
                InterstellarDiagnosticSeverity.Info);

            Messages.Message(
                "Корабль вышел на орбиту: " + ResolveNodeLabel(destination)
                + ". Режим: " + ResolveLandingModeLabel(record.preferredLandingMode) + ".",
                MessageTypeDefOf.PositiveEvent, false);

            if (Current.ProgramState == ProgramState.Playing)
                Find.WindowStack.Add(new Window_ShipLanding(record));
        }

        // ════════════════════════════════════════════════════════════════════
        //  Вспомогательные геттеры
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
            PlanetDefinition_IO sp = GalaxyConfigUtility.GetStartPlanet(galaxyConfig);
            OrbitalNode node = nodes.FirstOrDefault(n => n != null && n.id == sp?.id);
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
            if (ship == null)
                return GetDefaultHomeNodeId();

            ShipTransitRecord record = activeTravels.FirstOrDefault(
                t => t != null && t.shipThingId == ship.thingIDNumber);
            if (record != null)
            {
                return record.stage == InterstellarTransitStage.AwaitingLanding
                    ? (record.destinationId ?? record.sourceId)
                    : (record.sourceId ?? GetDefaultHomeNodeId());
            }

            ShipLocationRecord loc = FindLocationRecord(ship);
            if (loc != null && !string.IsNullOrEmpty(loc.currentNodeId))
            {
                if (ship.Spawned && ship.Map != null
                    && !string.IsNullOrEmpty(currentPlanetNodeId)
                    && GetNodeById(currentPlanetNodeId) != null
                    && loc.currentNodeId != currentPlanetNodeId)
                {
                    SetCurrentNodeForShip(ship, currentPlanetNodeId);
                    return currentPlanetNodeId;
                }

                return loc.currentNodeId;
            }

            if (ship.Spawned && ship.Map != null
                && !string.IsNullOrEmpty(currentPlanetNodeId)
                && GetNodeById(currentPlanetNodeId) != null)
            {
                SetCurrentNodeForShip(ship, currentPlanetNodeId);
                return currentPlanetNodeId;
            }

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

        private PlanetDefinition_IO FindPlanetDefById(string id)
        {
            if (string.IsNullOrEmpty(id) || galaxyConfig?.galaxies == null) return null;
            foreach (var g in galaxyConfig.galaxies)
                if (g?.planets != null)
                    foreach (var p in g.planets)
                        if (p != null && p.id == id) return p;
            return null;
        }

        private void EnsureLocationRecords()
        {
            shipLocations.RemoveAll(r => r == null);
            foreach (var travel in activeTravels)
            {
                if (travel == null || travel.shipThingId == 0) continue;
                if (FindLocationRecord(travel.shipThingId, travel.shipDefName) == null)
                    shipLocations.Add(new ShipLocationRecord
                    {
                        shipThingId   = travel.shipThingId,
                        shipDefName   = travel.shipDefName,
                        shipLabel     = travel.shipLabel,
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
            var byId = shipLocations.FirstOrDefault(r => r != null && r.shipThingId == id);
            if (byId != null) return byId;
            if (!string.IsNullOrEmpty(defName))
                return shipLocations.FirstOrDefault(r => r != null && r.shipThingId == 0 && r.shipDefName == defName);
            return null;
        }

        private void SetCurrentNodeForShip(Thing ship, string nodeId,
            string defName = null, string label = null, int fallbackId = 0)
        {
            int thingId = ship != null ? ship.thingIDNumber : fallbackId;
            string dn   = ship?.def?.defName ?? defName;
            string lbl  = ship != null ? ship.LabelCap : label;
            var loc = FindLocationRecord(thingId, dn);
            if (loc == null) { loc = new ShipLocationRecord(); shipLocations.Add(loc); }
            loc.shipThingId   = thingId;
            loc.shipDefName   = dn;
            loc.shipLabel     = lbl;
            loc.currentNodeId = string.IsNullOrEmpty(nodeId) ? GetDefaultHomeNodeId() : nodeId;
        }

        public void AddDiagnostic(string category, string title, string message,
            string details, InterstellarDiagnosticSeverity severity)
        {
            if (diagnostics == null) diagnostics = new List<InterstellarDiagnosticEntry>();
            diagnostics.Add(new InterstellarDiagnosticEntry
            {
                tick = Find.TickManager?.TicksGame ?? 0,
                category = category, title = title,
                message = message, details = details, severity = severity
            });
            if (diagnostics.Count > MaxDiagnostics)
                diagnostics.RemoveRange(0, diagnostics.Count - MaxDiagnostics);
        }

        public IEnumerable<InterstellarDiagnosticEntry> GetDiagnostics(string category = null)
        {
            if (diagnostics == null) yield break;
            for (int i = diagnostics.Count - 1; i >= 0; i--)
            {
                var e = diagnostics[i];
                if (e == null) continue;
                if (!string.IsNullOrEmpty(category) && category != "Все" && e.category != category) continue;
                yield return e;
            }
        }

        public string BuildDiagnosticsDump()
        {
            var sb = new StringBuilder();
            foreach (var e in GetDiagnostics())
            {
                sb.AppendLine("[" + e.severity + "] " + e.category + " | Tick " + e.tick);
                sb.AppendLine((e.title ?? "") + ": " + (e.message ?? ""));
                if (!string.IsNullOrEmpty(e.details)) sb.AppendLine(e.details);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public void ClearDiagnostics() => diagnostics?.Clear();

        private static GalaxyWorldConfiguration CloneConfig(GalaxyWorldConfiguration src)
        {
            if (src == null) return null;
            var copy = new GalaxyWorldConfiguration
            {
                selectedGalaxyCount = src.selectedGalaxyCount,
                galaxies = new List<GalaxyDefinition>()
            };
            if (src.galaxies == null) return copy;
            foreach (var g in src.galaxies)
            {
                if (g == null) continue;
                var gc = new GalaxyDefinition
                {
                    id = g.id, label = g.label, solarSystemId = g.solarSystemId,
                    hasStations = g.hasStations, hasAsteroidBelts = g.hasAsteroidBelts,
                    hasPlanets = g.hasPlanets, stationCount = g.stationCount,
                    beltCount = g.beltCount, planetCount = g.planetCount,
                    planets = new List<PlanetDefinition_IO>()
                };
                if (g.planets != null)
                    foreach (var p in g.planets)
                    {
                        if (p == null) continue;
                        gc.planets.Add(new PlanetDefinition_IO
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
                copy.galaxies.Add(gc);
            }
            return copy;
        }
    }
}
