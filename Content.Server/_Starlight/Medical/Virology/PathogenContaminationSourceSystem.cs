using System.Linq;
using Content.Server.Atmos.Rotting;
using Content.Server.Botany.Components;
using Content.Server.GameTicking;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Disposal.Unit;
using Content.Shared.Fluids.Components;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Samples physical station-neglect sources and derives the current contamination
/// snapshot directly from them.
/// </summary>
public sealed partial class PathogenContaminationSourceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private PathogenContaminationSystem _contamination = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private PathogenIsolationSystem _isolation = default!;
    [Dependency] private PathogenTransmissionSystem _transmission = default!;
    [Dependency] private RottingSystem _rotting = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, SourceSnapshot> _sources = new();
    private readonly Dictionary<EntityUid, TimeSpan> _suppressedUntil = new();
    private readonly HashSet<EntityUid> _nearby = new();

    private static readonly EntProtoId SporePatchPrototype = "PathogenSporePatch";
    private static readonly ProtoId<TagPrototype> OrganicTrashTag = "OrganicTrash";
    private static readonly ProtoId<ReagentPrototype> NutrimentReagent = "Nutriment";
    private static readonly ProtoId<ReagentPrototype> ProteinReagent = "Protein";
    private static readonly ProtoId<ReagentPrototype> MoldReagent = "Mold";
    private static readonly ProtoId<ReagentPrototype> WaterReagent = "Water";
    private static readonly PathogenType[] RotSignatures =
    [
        PathogenType.Bacteria,
        PathogenType.Fungus,
    ];

    private TimeSpan _nextSample;

    public int ActiveSourceCount => _sources.Count;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<BeingDisposedComponent, ComponentStartup>(OnBeingDisposed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        if (_timing.CurTime < _nextSample)
            return;

        SampleSources();
    }

    /// <summary>
    /// Rebuilds the physical-source snapshot immediately while in a live round.
    /// The same collection, contamination, and exposure rules as the timed sampler apply.
    /// </summary>
    public bool SampleNow()
    {
        if (_ticker.RunLevel != GameRunLevel.InRound)
            return false;

        SampleSources();
        return true;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _sources.Clear();
        _suppressedUntil.Clear();
        _nextSample = TimeSpan.Zero;
    }

    private void SampleSources()
    {
        var interval = Math.Max(
            1f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationSampleInterval));

        _nextSample = _timing.CurTime + TimeSpan.FromSeconds(interval);
        RemoveExpiredSuppressions();
        _sources.Clear();

        TryCreateSporePatches();
        CollectRotSources();
        CollectPuddleSources();
        CollectOrganicTrashSources();
        CollectDeadPlantSources();
        CollectSporeSources();

        RefreshContamination();
        InfectFromSources();
    }

    private void RefreshContamination()
    {
        var contributions = Enum.GetValues<PathogenType>()
            .ToDictionary(type => type, _ => 0f);

        foreach (var snapshot in _sources.Values)
        {
            var share = snapshot.Contamination / snapshot.PathogenTypes.Count;
            foreach (var type in snapshot.PathogenTypes)
            {
                contributions[type] += share;
            }
        }

        contributions[PathogenType.Virus] += GetViralCarrierContamination();

        _contamination.SetContamination(contributions);
    }

    private void CollectRotSources()
    {
        var corpseContamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationRottingCorpse));
        var foodContamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationRottenFood));
        var otherContamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationOtherRot));

        var query = EntityQueryEnumerator<RottingComponent, PerishableComponent>();
        while (query.MoveNext(out var uid, out _, out var perishable))
        {
            if (!_rotting.IsRotProgressing(uid, perishable))
                continue;

            if (HasComp<MobStateComponent>(uid))
            {
                AddSource(uid, RotSignatures, corpseContamination);
                continue;
            }

            if (HasComp<EdibleComponent>(uid))
            {
                AddSource(uid, RotSignatures, foodContamination);
                continue;
            }

            AddSource(uid, RotSignatures, otherContamination);
        }
    }

    private void CollectPuddleSources()
    {
        var biologicalPerUnit = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationBiologicalPuddlePerUnit));
        var biologicalMaximum = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationBiologicalPuddleMaximum));
        var foodPerUnit = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationFoodPuddlePerUnit));
        var foodMaximum = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationFoodPuddleMaximum));
        var waterPerUnit = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationWaterPuddlePerUnit));
        var waterMaximum = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationWaterPuddleMaximum));
        var waterMinimumVolume = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationWaterPuddleMinimumVolume));
        var moldPerUnit = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationMoldPuddlePerUnit));
        var moldMaximum = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationMoldPuddleMaximum));

        var query = EntityQueryEnumerator<PuddleComponent>();
        while (query.MoveNext(out var uid, out var puddle))
        {
            if (!_solutions.ResolveSolution(
                    uid,
                    puddle.SolutionName,
                    ref puddle.Solution,
                    out var solution))
            {
                continue;
            }

            var biologicalVolume = 0f;
            var foodVolume = 0f;
            var moldVolume = 0f;
            var waterVolume = 0f;
            foreach (var quantity in solution.Contents)
            {
                var reagentId = quantity.Reagent.Prototype;
                var volume = quantity.Quantity.Float();

                if (reagentId == NutrimentReagent || reagentId == ProteinReagent)
                    foodVolume += volume;

                if (reagentId == MoldReagent)
                    moldVolume += volume;

                if (reagentId == WaterReagent)
                    waterVolume += volume;

                if (_prototypes.TryIndex<ReagentPrototype>(reagentId, out var reagent) &&
                    reagent.Group == "Biological")
                {
                    biologicalVolume += volume;
                }
            }

            var biologicalContamination = PathogenContaminationMath.PuddleContamination(
                biologicalVolume,
                biologicalPerUnit,
                biologicalMaximum);
            AddSource(
                uid,
                [PathogenType.Bacteria],
                biologicalContamination,
                isPuddle: true);

            var foodContamination = PathogenContaminationMath.PuddleContamination(
                foodVolume,
                foodPerUnit,
                foodMaximum);
            AddSource(uid, [PathogenType.Bacteria], foodContamination);

            var moldContamination = PathogenContaminationMath.PuddleContamination(
                moldVolume,
                moldPerUnit,
                moldMaximum);
            AddSource(uid, [PathogenType.Fungus], moldContamination);

            // Standing water is the one floor source fungus gets, and blood is already
            // bacteria's. The volume gate matters: mopping leaves small water smears
            // behind, and cleaning up blood must not breed fungus as a side effect.
            if (waterVolume >= waterMinimumVolume)
            {
                var waterContamination = PathogenContaminationMath.PuddleContamination(
                    waterVolume,
                    waterPerUnit,
                    waterMaximum);
                AddSource(uid, [PathogenType.Fungus], waterContamination);
            }
        }
    }

    private void CollectOrganicTrashSources()
    {
        var contamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationOrganicTrash));

        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tags, out var transform))
        {
            if (transform.MapID == MapId.Nullspace ||
                transform.GridUid is null ||
                _containers.IsEntityInContainer(uid) ||
                !_tags.HasTag(tags, OrganicTrashTag))
            {
                continue;
            }

            AddSource(uid, [PathogenType.Bacteria], contamination);
        }
    }

    private void OnBeingDisposed(Entity<BeingDisposedComponent> entity, ref ComponentStartup args)
    {
        RemoveOrganicTrashTag(entity.Owner);
    }

    private void RemoveOrganicTrashTag(EntityUid uid)
    {
        _tags.RemoveTag(uid, OrganicTrashTag);

        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            RemoveOrganicTrashTag(child);
        }
    }

    private void CollectDeadPlantSources()
    {
        var contamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationDeadPlant));

        var query = EntityQueryEnumerator<PlantHolderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var plant, out var transform))
        {
            if (!plant.Dead ||
                plant.Seed is null ||
                transform.MapID == MapId.Nullspace ||
                transform.GridUid is null)
            {
                continue;
            }

            AddSource(uid, [PathogenType.Fungus], contamination);
        }
    }

    internal float GetViralCarrierContamination()
    {
        var perCarrier = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationViralCarrier));
        if (perCarrier <= 0f)
            return 0f;

        var carriers = 0;
        var query = EntityQueryEnumerator<PathogenInfectionComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var infections, out var mobState, out var transform))
        {
            if (mobState.CurrentState == MobState.Dead ||
                _isolation.IsIsolated(uid) ||
                transform.MapID == MapId.Nullspace ||
                transform.GridUid is null)
            {
                continue;
            }

            if (infections.Infections.Any(infection =>
                    _registry.TryGetStrain(infection.Pathogen, out var strain) &&
                    strain.PathogenType == PathogenType.Virus))
            {
                carriers++;
            }
        }

        return carriers * perCarrier;
    }

    private void CollectSporeSources()
    {
        var contamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologySporePatchContamination));
        var infectionChance = Math.Clamp(
            _config.GetCVar(StarlightCCVars.VirologySporePatchInfectionChance),
            0f,
            1f);

        var query = EntityQueryEnumerator<PathogenSporePatchComponent>();
        while (query.MoveNext(out var uid, out var patch))
        {
            if (patch.Strain <= 0 ||
                !_registry.TryGetStrain(patch.Strain, out var strain) ||
                strain.PathogenType != PathogenType.Fungus)
            {
                continue;
            }

            AddSource(
                uid,
                [PathogenType.Fungus],
                contamination,
                strainId: patch.Strain,
                minimumInfectionChance: infectionChance);
        }
    }

    internal void TryCreateSporePatches()
    {
        var chance = Math.Clamp(
            _config.GetCVar(StarlightCCVars.VirologySporePatchChance),
            0f,
            1f);
        var lifetime = Math.Max(
            1f,
            _config.GetCVar(StarlightCCVars.VirologySporePatchLifetime));
        var interval = TimeSpan.FromSeconds(Math.Max(
            1f,
            _config.GetCVar(StarlightCCVars.VirologySporePatchInterval)));

        if (chance <= 0f)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<PathogenInfectionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var infections, out var transform))
        {
            if (_isolation.IsIsolated(uid) ||
                transform.MapID == MapId.Nullspace ||
                transform.GridUid is null)
                continue;

            foreach (var infection in infections.Infections)
            {
                if (curTime < infection.NextSporePatch ||
                    !_registry.TryGetStrain(infection.Pathogen, out var strain) ||
                    strain.PathogenType != PathogenType.Fungus)
                {
                    continue;
                }

                // The cooldown advances whether or not the roll lands, so a carrier gets
                // one chance per interval rather than one per contamination sample.
                infection.NextSporePatch = curTime + interval;

                if (!_random.Prob(chance))
                    continue;

                var patch = Spawn(
                    SporePatchPrototype,
                    transform.Coordinates.SnapToGrid(EntityManager));
                Comp<PathogenSporePatchComponent>(patch).Strain = strain.Id;
                EnsureComp<TimedDespawnComponent>(patch).Lifetime = lifetime;
            }
        }
    }

    private void AddSource(
        EntityUid uid,
        IReadOnlyCollection<PathogenType> pathogenTypes,
        float contamination,
        bool isPuddle = false,
        int? strainId = null,
        float minimumInfectionChance = 0f)
    {
        if (contamination <= 0f ||
            pathogenTypes.Count == 0 ||
            pathogenTypes.Contains(PathogenType.Virus) ||
            _suppressedUntil.ContainsKey(uid))
        {
            return;
        }

        if (_sources.TryGetValue(uid, out var existing))
        {
            existing.PathogenTypes.UnionWith(pathogenTypes);
            existing.Contamination += contamination;
            existing.IsPuddle |= isPuddle;
            existing.StrainId ??= strainId;
            existing.MinimumInfectionChance = Math.Max(
                existing.MinimumInfectionChance,
                minimumInfectionChance);
            return;
        }

        _sources.Add(uid, new SourceSnapshot(
            pathogenTypes,
            contamination,
            isPuddle,
            strainId,
            minimumInfectionChance));
    }

    public bool TrySuppressSource(
        EntityUid source,
        TimeSpan duration,
        out IReadOnlyList<PathogenType> pathogenTypes)
    {
        if (!_sources.Remove(source, out var snapshot))
        {
            pathogenTypes = Array.Empty<PathogenType>();
            return false;
        }

        _suppressedUntil[source] = _timing.CurTime + duration;
        pathogenTypes = snapshot.PathogenTypes
            .OrderBy(type => type)
            .ToList();
        RefreshContamination();
        return true;
    }

    /// <summary>
    /// Resolves the most serious active strain represented by a physical source.
    /// Strain-pinned fungal patches always return their own strain.
    /// </summary>
    public bool TryGetSampleStrain(
        EntityUid source,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Pathogen? strain)
    {
        strain = null;
        if (!_sources.TryGetValue(source, out var snapshot))
            return false;

        var candidates = GetSourceStrains(snapshot, GetActiveStrains());
        strain = candidates
            .OrderByDescending(candidate => candidate.Tier)
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefault();
        return strain is not null;
    }

    public bool TryGetStrongestSource(
        EntityUid observer,
        out PathogenContaminationSourceReading reading)
    {
        reading = default;

        if (!TryComp(observer, out TransformComponent? observerTransform))
            return false;

        var observerPosition = _transform.GetWorldPosition(observerTransform);
        SourceSnapshot? strongest = null;
        EntityUid strongestUid = default;
        var strongestDistance = 0f;
        var strongestDirection = string.Empty;
        var strongestDangerous = false;
        var threshold = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationInfectionThreshold));

        foreach (var (source, snapshot) in _sources)
        {
            var dangerous = IsInfectionRisk(snapshot, threshold);
            if (!TryComp(source, out TransformComponent? sourceTransform) ||
                sourceTransform.MapID != observerTransform.MapID ||
                strongest is not null &&
                (strongestDangerous && !dangerous ||
                 strongestDangerous == dangerous &&
                 snapshot.Contamination <= strongest.Contamination))
            {
                continue;
            }

            var offset = _transform.GetWorldPosition(sourceTransform) - observerPosition;
            strongest = snapshot;
            strongestUid = source;
            strongestDistance = offset.Length();
            strongestDirection = GetDirection(offset.X, offset.Y);
            strongestDangerous = dangerous;
        }

        if (strongest is null)
            return false;

        reading = new PathogenContaminationSourceReading(
            strongestUid,
            strongest.PathogenTypes.OrderBy(type => type).ToList(),
            strongest.Contamination,
            strongestDistance,
            strongestDirection,
            FindNearestBeaconName(strongestUid),
            strongestDangerous);
        return true;
    }

    /// <summary>
    /// Resolves the active strains currently carried by one sampled physical source.
    /// A recognized source can return an empty list when no matching strain is active.
    /// </summary>
    public bool TryGetSourcePathogens(EntityUid source, out List<Pathogen> strains)
    {
        strains = new List<Pathogen>();
        if (!_sources.TryGetValue(source, out var snapshot))
            return false;

        strains = GetSourceStrains(snapshot, GetActiveStrains());
        return true;
    }

    public bool TryGetBeaconGroups(
        EntityUid observer,
        out EntityUid grid,
        out List<PathogenContaminationBeaconGroup> groups)
    {
        groups = [];
        grid = default;

        if (!TryComp(observer, out TransformComponent? observerTransform) ||
            observerTransform.GridUid is not { } observerGrid)
        {
            return false;
        }

        grid = observerGrid;
        var threshold = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationInfectionThreshold));
        var aggregates = new Dictionary<EntityUid, BeaconAggregate>();

        foreach (var (source, snapshot) in _sources)
        {
            if (!TryComp(source, out TransformComponent? sourceTransform) ||
                sourceTransform.GridUid != observerGrid ||
                !TryFindNearestBeacon(source, out var beaconUid, out var beacon))
            {
                continue;
            }

            if (!aggregates.TryGetValue(beaconUid, out var aggregate))
            {
                aggregate = new BeaconAggregate(GetBeaconName(beaconUid, beacon));
                aggregates.Add(beaconUid, aggregate);
            }

            aggregate.Total += snapshot.Contamination;
            aggregate.SourceCount++;
            if (IsInfectionRisk(snapshot, threshold))
                aggregate.InfectiousSourceCount++;

            var share = snapshot.Contamination / snapshot.PathogenTypes.Count;
            foreach (var type in snapshot.PathogenTypes)
            {
                if (type == PathogenType.Bacteria)
                    aggregate.Bacteria += share;
                else if (type == PathogenType.Fungus)
                    aggregate.Fungus += share;
            }
        }

        groups = aggregates
            .Select(pair =>
            {
                var aggregate = pair.Value;
                return new PathogenContaminationBeaconGroup(
                    GetNetEntity(pair.Key),
                    GetNetCoordinates(Transform(pair.Key).Coordinates),
                    aggregate.Name,
                    aggregate.Total,
                    aggregate.Bacteria,
                    aggregate.Fungus,
                    aggregate.SourceCount,
                    aggregate.InfectiousSourceCount);
            })
            .OrderByDescending(group => group.InfectiousSourceCount > 0)
            .ThenByDescending(group => group.Total)
            .ThenBy(group => group.BeaconName)
            .ToList();
        return true;
    }

    private void InfectFromSources()
    {
        if (_sources.Count == 0)
            return;

        var radius = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationInfectionRadius));
        var threshold = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationInfectionThreshold));
        var chanceScale = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationInfectionChanceScale));
        var puddleChance = Math.Clamp(
            _config.GetCVar(StarlightCCVars.VirologyContaminationPuddleInfectionChance),
            0f,
            1f);

        if (radius <= 0f)
            return;

        var active = GetActiveStrains();
        foreach (var (source, snapshot) in _sources)
        {
            if (!TryComp(source, out TransformComponent? transform))
                continue;

            var minimumChance = Math.Max(
                snapshot.MinimumInfectionChance,
                snapshot.IsPuddle ? puddleChance : 0f);
            var chance = PathogenContaminationMath.SourceInfectionChance(
                snapshot.Contamination,
                threshold,
                chanceScale,
                minimumChance);

            if (chance <= 0f)
                continue;

            var strains = GetSourceStrains(snapshot, active);
            if (strains.Count == 0)
                continue;

            _nearby.Clear();
            _lookup.GetEntitiesInRange(
                transform.Coordinates,
                radius,
                _nearby,
                LookupFlags.Uncontained);

            var targets = _nearby
                .Where(target =>
                    target != source &&
                    _interaction.InRangeUnobstructed(
                        source,
                        target,
                        radius,
                        overlapCheck: false))
                .ToList();
            _random.Shuffle(targets);

            foreach (var target in targets)
            {
                var eligible = strains
                    .Where(strain => _transmission.CanExpose(target, strain))
                    .ToList();

                if (eligible.Count == 0)
                    continue;

                var strain = _random.Pick(eligible);
                if (_transmission.TryExpose(target, strain, chance))
                    break;
            }
        }
    }

    private Dictionary<PathogenType, List<Pathogen>> GetActiveStrains()
    {
        var active = Enum.GetValues<PathogenType>()
            .ToDictionary(type => type, _ => new List<Pathogen>());
        var seen = new HashSet<int>();

        var query = EntityQueryEnumerator<PathogenInfectionComponent>();
        while (query.MoveNext(out _, out var infections))
        {
            foreach (var infection in infections.Infections)
            {
                if (!seen.Add(infection.Pathogen) ||
                    !_registry.TryGetStrain(infection.Pathogen, out var strain))
                {
                    continue;
                }

                active[strain.PathogenType].Add(strain);
            }
        }

        return active;
    }

    private List<Pathogen> GetSourceStrains(
        SourceSnapshot snapshot,
        IReadOnlyDictionary<PathogenType, List<Pathogen>> active)
    {
        if (snapshot.StrainId is { } strainId)
        {
            if (_registry.TryGetStrain(strainId, out var pinned) &&
                snapshot.PathogenTypes.Contains(pinned.PathogenType))
            {
                return [pinned];
            }

            return [];
        }

        return snapshot.PathogenTypes
            .SelectMany(type => active[type])
            .ToList();
    }

    private string? FindNearestBeaconName(EntityUid source)
    {
        if (!TryFindNearestBeacon(source, out var beaconUid, out var beacon))
            return null;

        return GetBeaconName(beaconUid, beacon);
    }

    private bool TryFindNearestBeacon(
        EntityUid source,
        out EntityUid nearestUid,
        out NavMapBeaconComponent nearest)
    {
        nearestUid = default;
        nearest = default!;

        if (!TryComp(source, out TransformComponent? sourceTransform) ||
            sourceTransform.GridUid is null)
        {
            return false;
        }

        var sourcePosition = _transform.GetWorldPosition(sourceTransform);
        var nearestDistance = float.MaxValue;
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var transform))
        {
            if (!beacon.Enabled ||
                !transform.Anchored ||
                transform.GridUid != sourceTransform.GridUid)
            {
                continue;
            }

            var distance = (_transform.GetWorldPosition(transform) - sourcePosition).Length();
            if (distance >= nearestDistance)
                continue;

            nearest = beacon;
            nearestUid = uid;
            nearestDistance = distance;
        }

        return nearestUid.IsValid();
    }

    private string GetBeaconName(EntityUid uid, NavMapBeaconComponent beacon)
    {
        if (!string.IsNullOrWhiteSpace(beacon.Text))
            return beacon.Text;

        if (beacon.DefaultText is { } defaultText)
            return Loc.GetString(defaultText);

        return Name(uid);
    }

    private static bool IsInfectionRisk(SourceSnapshot snapshot, float threshold)
        => snapshot.Contamination >= threshold ||
           snapshot.IsPuddle ||
           snapshot.MinimumInfectionChance > 0f;

    private void RemoveExpiredSuppressions()
    {
        foreach (var (source, expiry) in _suppressedUntil.ToArray())
        {
            if (_timing.CurTime < expiry && Exists(source))
                continue;

            _suppressedUntil.Remove(source);
        }
    }

    private static string GetDirection(float x, float y)
    {
        const float diagonalRatio = 0.4f;

        if (Math.Abs(x) < 0.01f && Math.Abs(y) < 0.01f)
            return "here";

        var horizontal = Math.Abs(x) > Math.Abs(y) * diagonalRatio
            ? x >= 0f ? "east" : "west"
            : string.Empty;
        var vertical = Math.Abs(y) > Math.Abs(x) * diagonalRatio
            ? y >= 0f ? "north" : "south"
            : string.Empty;

        if (vertical.Length == 0)
            return horizontal;
        if (horizontal.Length == 0)
            return vertical;

        return $"{vertical}-{horizontal}";
    }

    private sealed class SourceSnapshot
    {
        public readonly HashSet<PathogenType> PathogenTypes;
        public float Contamination;
        public bool IsPuddle;
        public int? StrainId;
        public float MinimumInfectionChance;

        public SourceSnapshot(
            IReadOnlyCollection<PathogenType> pathogenTypes,
            float contamination,
            bool isPuddle,
            int? strainId,
            float minimumInfectionChance)
        {
            PathogenTypes = new HashSet<PathogenType>(pathogenTypes);
            Contamination = contamination;
            IsPuddle = isPuddle;
            StrainId = strainId;
            MinimumInfectionChance = minimumInfectionChance;
        }
    }

    private sealed class BeaconAggregate(string name)
    {
        public readonly string Name = name;
        public float Total;
        public float Bacteria;
        public float Fungus;
        public int SourceCount;
        public int InfectiousSourceCount;
    }
}

public readonly record struct PathogenContaminationSourceReading(
    EntityUid Source,
    IReadOnlyList<PathogenType> PathogenTypes,
    float Contamination,
    float Distance,
    string Direction,
    string? BeaconName,
    bool InfectionRisk);

internal static class PathogenContaminationMath
{
    public static float PuddleContamination(float biologicalVolume, float perUnit, float maximum)
        => Math.Clamp(Math.Max(0f, biologicalVolume) * Math.Max(0f, perUnit), 0f, Math.Max(0f, maximum));

    public static float SourceInfectionChance(
        float contamination,
        float threshold,
        float chanceScale,
        float minimumChance)
    {
        var scaled = contamination >= Math.Max(0f, threshold)
            ? Math.Max(0f, contamination) * Math.Max(0f, chanceScale)
            : 0f;

        return Math.Clamp(Math.Max(scaled, Math.Max(0f, minimumChance)), 0f, 1f);
    }
}
