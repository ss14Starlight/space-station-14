using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Owns every strain that exists this round. Strains are rolled from archetypes rather
/// than authored, so this is the only place that knows what a given strain actually is -
/// everything else refers to them by id.
///
/// Cleared on round restart. Strain ids are meaningless across rounds.
/// </summary>
public sealed partial class PathogenRegistrySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string DesignationLetters = "ABCDEFGHJKLMNPRSTVWXYZ";

    private readonly Dictionary<int, Pathogen> _strains = new();

    private int _nextId = 1;

    public IReadOnlyDictionary<int, Pathogen> Strains => _strains;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _strains.Clear();
        _nextId = 1;
    }

    /// <summary>
    /// Rolls a new strain from an archetype and registers it for the rest of the round.
    /// </summary>
    public Pathogen? Generate(ProtoId<PathogenArchetypePrototype> archetypeId)
    {
        return !_proto.TryIndex(archetypeId, out var archetype) ? null : Generate(archetype);
    }

    public Pathogen Generate(PathogenArchetypePrototype archetype)
        => Generate(archetype, new PathogenGenerationOptions());

    internal Pathogen Generate(
        PathogenArchetypePrototype archetype,
        PathogenGenerationOptions options)
    {
        var transmissibilityMultiplier = Math.Max(0f, options.TransmissibilityMultiplier);
        var protectionBypassMultiplier = Math.Max(0f, options.ProtectionBypassMultiplier);
        var stageDelayMultiplier = Math.Max(0f, options.StageDelayMultiplier);

        var strain = new Pathogen
        {
            Id = _nextId++,
            Designation = RollDesignation(),
            Archetype = archetype.ID,
            PathogenType = archetype.PathogenType,
            Tier = archetype.Tier,
            Beneficial = archetype.Beneficial,
            ProtectionBypass = Math.Clamp(
                archetype.ProtectionBypass * protectionBypassMultiplier,
                0f,
                1f),
            ImmunityOnRecovery = archetype.ImmunityOnRecovery,
            RespawnOnExtinction = archetype.RespawnOnExtinction,
            MaxPrevalence = Math.Min(
                archetype.MaxPrevalence,
                Math.Max(0f, options.MaxPrevalenceCap)),
            Transmissibility =
                _random.NextFloat(archetype.MinTransmissibility, archetype.MaxTransmissibility) *
                transmissibilityMultiplier,
            SpreadRange = _random.NextFloat(archetype.MinSpreadRange, archetype.MaxSpreadRange),
            Incubation = RollTime(archetype.MinIncubation, archetype.MaxIncubation),
            MaxStage = _random.Next(archetype.MinStages, archetype.MaxStages + 1),
            StageDelay = ScaleTime(
                RollTime(archetype.MinStageDelay, archetype.MaxStageDelay),
                stageDelayMultiplier),
            Duration = RollTime(archetype.MinDuration, archetype.MaxDuration),
            Symptoms = RollSymptoms(archetype, options),
        };

        _strains[strain.Id] = strain;

        return strain;
    }

    public bool TryGetStrain(int id, [NotNullWhen(true)] out Pathogen? strain)
        => _strains.TryGetValue(id, out strain);

    public PathogenAnalysisResult CanAnalyzeSample(
        int strainId,
        EntityUid? host,
        bool sourceSample)
    {
        if (!TryGetStrain(strainId, out var strain))
            return PathogenAnalysisResult.Invalid;

        if (sourceSample)
            return PathogenAnalysisResult.Accepted;

        if (host is not { } patient)
            return PathogenAnalysisResult.Invalid;

        // The distinct-host rule only exists to stop one patient being swabbed twice to
        // skip half the identification. Once the strain is fully identified it serves no
        // purpose, and enforcing it would make replacing a lost or stolen culture
        // needlessly painful - identification lives on the strain, so a single fresh
        // sample should be enough to grow another.
        if (strain.Identification == PathogenIdentificationStage.Complete)
            return PathogenAnalysisResult.Accepted;

        return strain.SampledHosts.Contains(patient)
            ? PathogenAnalysisResult.DuplicateHost
            : PathogenAnalysisResult.Accepted;
    }

    /// <summary>
    /// Advances station-wide identification. Patient specimens require two distinct
    /// hosts; a source specimen completes the strain immediately.
    /// </summary>
    public PathogenAnalysisResult AnalyzeSample(
        int strainId,
        EntityUid? host,
        bool sourceSample)
    {
        var allowed = CanAnalyzeSample(strainId, host, sourceSample);
        if (allowed != PathogenAnalysisResult.Accepted ||
            !TryGetStrain(strainId, out var strain))
        {
            return allowed;
        }

        if (sourceSample)
        {
            var completedNow = strain.Identification != PathogenIdentificationStage.Complete;
            strain.Identification = PathogenIdentificationStage.Complete;
            return completedNow
                ? PathogenAnalysisResult.Completed
                : PathogenAnalysisResult.AlreadyComplete;
        }

        var patient = host!.Value;
        strain.SampledHosts.Add(patient);

        if (strain.Identification == PathogenIdentificationStage.Unidentified)
        {
            strain.Identification = PathogenIdentificationStage.Partial;
            return PathogenAnalysisResult.Partial;
        }

        if (strain.Identification == PathogenIdentificationStage.Partial)
        {
            strain.Identification = PathogenIdentificationStage.Complete;
            return PathogenAnalysisResult.Completed;
        }

        return PathogenAnalysisResult.AlreadyComplete;
    }

    public bool IdentifyFully(int strainId)
    {
        if (!TryGetStrain(strainId, out var strain))
            return false;

        strain.Identification = PathogenIdentificationStage.Complete;
        return true;
    }

    /// <summary>
    /// Core symptoms always, then a random draw from the pool. The cores are what keep an
    /// archetype recognisable; the draw is what stops it being memorisable.
    /// </summary>
    private List<ProtoId<PathogenSymptomPrototype>> RollSymptoms(
        PathogenArchetypePrototype archetype,
        PathogenGenerationOptions options)
    {
        var symptoms = new List<ProtoId<PathogenSymptomPrototype>>(archetype.CoreSymptoms);

        var pool = archetype.SymptomPool
            .Where(x => !symptoms.Contains(x))
            .ToList();

        var minimum = Math.Max(0, options.MinExtraSymptoms ?? archetype.MinExtraSymptoms);
        var maximum = Math.Max(minimum, options.MaxExtraSymptoms ?? archetype.MaxExtraSymptoms);
        var wanted = _random.Next(minimum, maximum + 1);
        var count = Math.Min(wanted, pool.Count);

        for (var i = 0; i < count; i++)
        {
            var index = _random.Next(pool.Count);
            symptoms.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return symptoms;
    }

    private TimeSpan RollTime(TimeSpan min, TimeSpan max)
    {
        if (max <= min)
            return min;

        return TimeSpan.FromSeconds(_random.NextFloat((float) min.TotalSeconds, (float) max.TotalSeconds));
    }

    private static TimeSpan ScaleTime(TimeSpan time, float multiplier)
        => TimeSpan.FromSeconds(time.TotalSeconds * multiplier);

    /// <summary>
    /// Two letters and three digits, so the crew has something to actually call it.
    /// Ambiguous letters are left out so nobody misreads it over the radio.
    /// </summary>
    private string RollDesignation()
    {
        var first = DesignationLetters[_random.Next(DesignationLetters.Length)];
        var second = DesignationLetters[_random.Next(DesignationLetters.Length)];

        return $"{first}{second}-{_random.Next(100, 1000)}";
    }
}

internal sealed class PathogenGenerationOptions
{
    public float MaxPrevalenceCap = 1f;
    public float TransmissibilityMultiplier = 1f;
    public float ProtectionBypassMultiplier = 1f;
    public float StageDelayMultiplier = 1f;
    public int? MinExtraSymptoms;
    public int? MaxExtraSymptoms;
}

public enum PathogenAnalysisResult : byte
{
    Invalid,
    DuplicateHost,
    Accepted,
    Partial,
    Completed,
    AlreadyComplete,
}
