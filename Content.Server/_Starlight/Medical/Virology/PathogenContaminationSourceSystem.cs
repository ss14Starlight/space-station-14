using System.Linq;
using Content.Server.Atmos.Rotting;
using Content.Server.GameTicking;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Disposal.Unit;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
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
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PathogenContaminationSystem _contamination = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private RottingSystem _rotting = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private TagSystem _tags = default!;

    private readonly Dictionary<EntityUid, SourceSnapshot> _sources = new();
    private readonly HashSet<EntityUid> _baselineSources = new();
    private readonly HashSet<EntityUid> _ignoredBaselineSources = new();
    private readonly Dictionary<PathogenContaminationSourceKind, SourceReportBuilder> _sourceReport = new();
    private readonly Dictionary<PathogenContaminationSourceKind, SourceReportBuilder> _ignoredBaselineReport = new();
    private readonly List<PathogenContaminationSourceReport> _lastSourceReport = new();
    private readonly List<PathogenContaminationSourceReport> _lastIgnoredBaselineReport = new();

    private static readonly ProtoId<TagPrototype> OrganicTrashTag = "OrganicTrash";
    private static readonly ProtoId<TagPrototype> RottenFoodTag = "PathogenRottenFood";
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
    private bool _hasBaseline;

    public int ActiveSourceCount => _sources.Count;
    public int BaselineSourceCount => _baselineSources.Count;
    public int IgnoredBaselineSourceCount => _ignoredBaselineSources.Count;
    public bool HasBaseline => _hasBaseline;
    public IReadOnlyList<PathogenContaminationSourceReport> SourceReport => _lastSourceReport;
    public IReadOnlyList<PathogenContaminationSourceReport> IgnoredBaselineReport => _lastIgnoredBaselineReport;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<BeingDisposedComponent, ComponentStartup>(OnBeingDisposed);
    }

    /// <summary>
    /// The baseline is taken the instant the round starts.
    /// </summary>
    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound || _hasBaseline)
            return;

        SampleSources();
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
    /// </summary>
    public bool SampleNow()
    {
        if (_ticker.RunLevel != GameRunLevel.InRound)
            return false;

        SampleSources();
        return true;
    }

    /// <summary>
    /// Rebuilds the source snapshot without the live-round gate <see cref="SampleNow"/>
    /// enforces for admin use. Integration tests have no round, so they cannot reach the
    /// collectors any other way.
    /// </summary>
    internal void SampleSourcesForTest()
        => SampleSources();

    internal void ResetSourceStateForTest()
        => ResetSourceState();

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
        => ResetSourceState();

    private void ResetSourceState()
    {
        _sources.Clear();
        _baselineSources.Clear();
        _ignoredBaselineSources.Clear();
        _sourceReport.Clear();
        _ignoredBaselineReport.Clear();
        _lastSourceReport.Clear();
        _lastIgnoredBaselineReport.Clear();
        _nextSample = TimeSpan.Zero;
        _hasBaseline = false;
    }

    private void SampleSources()
    {
        var interval = Math.Max(
            1f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationSampleInterval));

        _nextSample = _timing.CurTime + TimeSpan.FromSeconds(interval);
        _sources.Clear();
        _ignoredBaselineSources.Clear();
        _sourceReport.Clear();
        _ignoredBaselineReport.Clear();

        CollectRotSources();
        CollectRottenFoodSources();
        CollectPuddleSources();
        CollectOrganicTrashSources();
        CollectDeadPlantSources();

        _hasBaseline = true;
        PublishSourceReport(_sourceReport, _lastSourceReport);
        PublishSourceReport(_ignoredBaselineReport, _lastIgnoredBaselineReport);
        RefreshContamination();
    }

    private void RefreshContamination()
    {
        var contributions = Enum.GetValues<PathogenType>()
            .ToDictionary(type => type, _ => 0f);

        foreach (var snapshot in _sources.Values)
        {
            foreach (var (type, value) in snapshot.Contributions)
                contributions[type] += value;
        }

        contributions[PathogenType.Virus] += GetViralCarrierContamination();

        _contamination.SetContamination(contributions);
    }

    /// <summary>
    /// Records one physical source. <paramref name="contamination"/> is split evenly across
    /// <paramref name="pathogenTypes"/> unless <paramref name="perType"/> is set, in which
    /// case each type receives the full amount and the source contributes that much again
    /// for every type it feeds.
    /// </summary>
    private void AddSource(
        EntityUid uid,
        IReadOnlyCollection<PathogenType> pathogenTypes,
        float contamination,
        PathogenContaminationSourceKind kind,
        bool perType = false)
    {
        if (contamination <= 0f ||
            pathogenTypes.Count == 0 ||
            pathogenTypes.Contains(PathogenType.Virus) ||
            HasComp<PathogenDisposedComponent>(uid))
        {
            return;
        }

        if (!_hasBaseline)
        {
            _baselineSources.Add(uid);
            _ignoredBaselineSources.Add(uid);
            RecordSource(_ignoredBaselineReport, kind, contamination);
            return;
        }

        if (_baselineSources.Contains(uid))
        {
            _ignoredBaselineSources.Add(uid);
            RecordSource(_ignoredBaselineReport, kind, contamination);
            return;
        }

        RecordSource(_sourceReport, kind, contamination);

        if (_sources.TryGetValue(uid, out var existing))
        {
            existing.Add(pathogenTypes, contamination, perType);
            return;
        }

        _sources.Add(uid, new SourceSnapshot(pathogenTypes, contamination, perType));
    }

    private static void RecordSource(
        Dictionary<PathogenContaminationSourceKind, SourceReportBuilder> report,
        PathogenContaminationSourceKind kind,
        float contamination)
    {
        if (!report.TryGetValue(kind, out var builder))
        {
            report.Add(kind, new SourceReportBuilder(1, contamination));
            return;
        }

        builder.Count++;
        builder.Contamination += contamination;
    }

    private static void PublishSourceReport(
        Dictionary<PathogenContaminationSourceKind, SourceReportBuilder> source,
        List<PathogenContaminationSourceReport> target)
    {
        target.Clear();

        foreach (var (kind, builder) in source.OrderBy(entry => entry.Key))
        {
            target.Add(new PathogenContaminationSourceReport(
                kind,
                builder.Count,
                builder.Contamination));
        }
    }

    private sealed class SourceSnapshot
    {
        public readonly Dictionary<PathogenType, float> Contributions = new();

        public SourceSnapshot(
            IReadOnlyCollection<PathogenType> pathogenTypes,
            float contamination,
            bool perType)
        {
            Add(pathogenTypes, contamination, perType);
        }

        public void Add(
            IReadOnlyCollection<PathogenType> pathogenTypes,
            float contamination,
            bool perType)
        {
            var share = perType ? contamination : contamination / pathogenTypes.Count;
            foreach (var type in pathogenTypes)
            {
                Contributions[type] = Contributions.TryGetValue(type, out var existing)
                    ? existing + share
                    : share;
            }
        }
    }

    private sealed class SourceReportBuilder
    {
        public int Count;
        public float Contamination;

        public SourceReportBuilder(int count, float contamination)
        {
            Count = count;
            Contamination = contamination;
        }
    }
}

public enum PathogenContaminationSourceKind : byte
{
    RottingCorpse,
    RottingEdible,
    SpoiledFood,
    BiologicalPuddle,
    FoodPuddle,
    MoldPuddle,
    WaterPuddle,
    OrganicTrash,
    DeadPlant,
}

public readonly record struct PathogenContaminationSourceReport(
    PathogenContaminationSourceKind Kind,
    int Count,
    float Contamination);

internal static class PathogenContaminationMath
{
    public static float PuddleContamination(float biologicalVolume, float perUnit, float maximum)
        => Math.Clamp(Math.Max(0f, biologicalVolume) * Math.Max(0f, perUnit), 0f, Math.Max(0f, maximum));
}
