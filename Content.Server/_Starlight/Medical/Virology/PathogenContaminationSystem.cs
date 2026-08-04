using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Seeds the round's ambient disease pool, then owns station contamination and turns
/// its 25/50/75 milestones into additional pathogen outbreaks.
/// </summary>
public sealed partial class PathogenContaminationSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PathogenHostSelectionSystem _hosts = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private PathogenSystem _pathogen = default!;

    private readonly PathogenContaminationMilestones _milestones = new();
    private readonly PathogenContaminationPool _contamination = new();
    private readonly HashSet<PathogenType> _ambientTypes = new();
    private readonly HashSet<EntityUid> _seededHosts = new();

    private EmergentSeedState? _emergentSeed;

    public float Contamination => _contamination.Total;

    public float GetContamination(PathogenType type)
        => _contamination.Get(type);

    public IReadOnlyList<PathogenType> GetDominantTypes()
        => _contamination.GetDominantTypes();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        MaintainEmergentSeeds();
    }

    public void SetContamination(IReadOnlyDictionary<PathogenType, float> contributions)
    {
        _contamination.Set(contributions);
        ProcessMilestones();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _contamination.Reset();
        _milestones.Reset();
        _ambientTypes.Clear();
        _seededHosts.Clear();
        _emergentSeed = null;
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
            return;

        for (var i = 0; i < 2; i++)
        {
            if (!TrySpawnAmbient(null, $"round-start seed {i + 1}"))
                Log.Warning($"Could not create round-start ambient pathogen {i + 1}.");
        }
    }

    private void ProcessMilestones()
    {
        var preferredType = PickDominantType();

        foreach (var milestone in _milestones.GetPending(Contamination))
        {
            var handled = milestone switch
            {
                PathogenContaminationMilestone.AmbientLow or
                    PathogenContaminationMilestone.AmbientHigh =>
                    TrySpawnAmbient(preferredType, $"contamination milestone {milestone}"),
                PathogenContaminationMilestone.Emergent =>
                    TrySpawnEmergent(preferredType),
                _ => false,
            };

            // A failed antag chance is final. Missing content or hosts stays pending so a
            // guaranteed outbreak is not silently lost.
            if (handled)
                _milestones.MarkHandled(milestone);
        }
    }

    private PathogenType? PickDominantType()
    {
        var dominant = _contamination.GetDominantTypes();
        return dominant.Count switch
        {
            0 => null,
            1 => dominant[0],
            _ => _random.Pick(dominant),
        };
    }

    private bool TrySpawnAmbient(
        PathogenType? preferredType,
        string cause)
    {
        if (!TryPickAmbientArchetype(preferredType, out var archetype) ||
            !TrySeed(archetype, null, out var strain, out var host))
        {
            Log.Warning($"Could not seed the ambient pathogen for {cause}.");
            return false;
        }

        _ambientTypes.Add(archetype.PathogenType);
        _adminLog.Add(LogType.EventStarted,
            LogImpact.Low,
            $"{cause} seeded ambient pathogen {strain.Archetype} " +
            $"{strain.Designation} ({strain.PathogenType}) on {ToPrettyString(host):host}");
        return true;
    }

    private bool TrySpawnEmergent(PathogenType? preferredType)
    {
        var hasBioterrorThreat = HasBioterrorThreat();
        var spawnChance = Math.Clamp(
            _config.GetCVar(StarlightCCVars.VirologyEmergentAntagChance),
            0f,
            1f);

        if (hasBioterrorThreat && !_random.Prob(spawnChance))
        {
            _adminLog.Add(LogType.EventStarted,
                LogImpact.Low,
                $"The one-time contamination emergent roll failed during a bioterror round.");
            return true;
        }

        if (!TryPickArchetype(PathogenTier.Emergent, preferredType, false, out var archetype))
        {
            Log.Warning("No emergent pathogen archetype is available for the contamination milestone.");
            return false;
        }

        var options = hasBioterrorThreat ? GetReducedEmergentOptions() : null;

        if (!TrySeedEmergent(archetype, options, out var strain, out var hosts))
        {
            Log.Warning("The contamination emergent roll succeeded, but no valid host was available.");
            return false;
        }

        var profile = hasBioterrorThreat ? "reduced" : "full";
        _adminLog.Add(LogType.EventStarted,
            LogImpact.Medium,
            $"Contamination seeded {profile} emergent pathogen {strain.Archetype} " +
            $"{strain.Designation} ({strain.PathogenType}) on {hosts.Count} initial hosts");
        return true;
    }

    private PathogenGenerationOptions GetReducedEmergentOptions()
    {
        return new PathogenGenerationOptions
        {
            MaxPrevalenceCap = _config.GetCVar(StarlightCCVars.VirologyEmergentAntagPrevalenceCap),
            TransmissibilityMultiplier =
                _config.GetCVar(StarlightCCVars.VirologyEmergentAntagTransmissibilityMultiplier),
            ProtectionBypassMultiplier =
                _config.GetCVar(StarlightCCVars.VirologyEmergentAntagProtectionBypassMultiplier),
            StageDelayMultiplier =
                _config.GetCVar(StarlightCCVars.VirologyEmergentAntagStageDelayMultiplier),
            MinExtraSymptoms =
                _config.GetCVar(StarlightCCVars.VirologyEmergentAntagMinExtraSymptoms),
            MaxExtraSymptoms =
                _config.GetCVar(StarlightCCVars.VirologyEmergentAntagMaxExtraSymptoms),
        };
    }

    private bool TryPickAmbientArchetype(
        PathogenType? preferredType,
        out PathogenArchetypePrototype archetype)
    {
        if (TryPickArchetype(PathogenTier.Ambient, preferredType, true, out archetype))
            return true;

        // Four ambient strains can exist but there are only three pathogen types.
        // Preserve type variety until every type is represented, then permit repeats.
        return TryPickArchetype(PathogenTier.Ambient, preferredType, false, out archetype);
    }

    private bool TryPickArchetype(
        PathogenTier tier,
        PathogenType? preferredType,
        bool excludeAmbientTypes,
        out PathogenArchetypePrototype archetype)
    {
        var candidates = new List<PathogenArchetypePrototype>();

        foreach (var candidate in _proto.EnumeratePrototypes<PathogenArchetypePrototype>())
        {
            if (candidate.Beneficial || candidate.Tier != tier)
                continue;

            if (tier == PathogenTier.Ambient && candidate.SeedWeight <= 0f)
                continue;

            if (excludeAmbientTypes && _ambientTypes.Contains(candidate.PathogenType))
                continue;

            candidates.Add(candidate);
        }

        if (preferredType is not null)
        {
            var preferred = candidates
                .Where(candidate => candidate.PathogenType == preferredType)
                .ToList();

            if (preferred.Count > 0)
                candidates = preferred;
        }

        if (candidates.Count == 0)
        {
            archetype = default!;
            return false;
        }

        if (tier != PathogenTier.Ambient)
        {
            archetype = _random.Pick(candidates);
            return true;
        }

        var totalWeight = candidates.Sum(candidate => candidate.SeedWeight);
        var roll = _random.NextFloat(0f, totalWeight);

        foreach (var candidate in candidates)
        {
            roll -= candidate.SeedWeight;
            if (roll > 0f)
                continue;

            archetype = candidate;
            return true;
        }

        archetype = candidates[^1];
        return true;
    }

    private bool TrySeed(
        PathogenArchetypePrototype archetype,
        PathogenGenerationOptions? options,
        out Pathogen strain,
        out EntityUid host)
    {
        var hosts = _hosts.GetEligibleAutomaticHosts();
        if (hosts.Count == 0)
        {
            strain = default!;
            host = default;
            return false;
        }

        var freshHosts = hosts.Where(candidate => !_seededHosts.Contains(candidate)).ToList();
        host = _random.Pick(freshHosts.Count > 0 ? freshHosts : hosts);
        strain = options is null
            ? _registry.Generate(archetype)
            : _registry.Generate(archetype, options);

        if (!_pathogen.TryInfect(host, strain.Id))
            return false;

        _seededHosts.Add(host);
        return true;
    }

    private bool TrySeedEmergent(
        PathogenArchetypePrototype archetype,
        PathogenGenerationOptions? options,
        out Pathogen strain,
        out List<EntityUid> hosts)
    {
        hosts = _hosts.GetEligibleAutomaticHosts();
        if (hosts.Count == 0)
        {
            strain = default!;
            return false;
        }

        _random.Shuffle(hosts);
        hosts = hosts
            .OrderBy(candidate => _seededHosts.Contains(candidate))
            .ToList();

        strain = options is null
            ? _registry.Generate(archetype)
            : _registry.Generate(archetype, options);

        var targetCount = Math.Min(2, hosts.Count);
        hosts.RemoveRange(targetCount, hosts.Count - targetCount);

        for (var i = hosts.Count - 1; i >= 0; i--)
        {
            if (!_pathogen.TryInfect(hosts[i], strain.Id))
            {
                hosts.RemoveAt(i);
                continue;
            }

            _seededHosts.Add(hosts[i]);
        }

        if (hosts.Count == 0)
            return false;

        _emergentSeed = new EmergentSeedState(strain.Id, hosts);
        return true;
    }

    private void MaintainEmergentSeeds()
    {
        if (_emergentSeed is not { } state)
            return;

        foreach (var carrier in state.Carriers)
        {
            if (!HasReachedSymptomaticStage(carrier.Host, state.Strain))
                continue;

            _emergentSeed = null;
            return;
        }

        for (var i = state.Carriers.Count - 1; i >= 0; i--)
        {
            var carrier = state.Carriers[i];

            // A real cure is respected. Replacement only protects the outbreak from
            // unavailable initial players before symptoms begin.
            if (!_pathogen.IsInfected(carrier.Host, state.Strain))
            {
                state.Carriers.RemoveAt(i);
                continue;
            }

            if (_hosts.IsEligibleAutomaticHost(carrier.Host))
                continue;

            _pathogen.Cure(carrier.Host, state.Strain);

            if (carrier.Replaced ||
                !TryPickEmergentReplacement(state, out var replacement) ||
                !_pathogen.TryInfect(replacement, state.Strain))
            {
                state.Carriers.RemoveAt(i);
                continue;
            }

            carrier.Host = replacement;
            carrier.Replaced = true;
            _seededHosts.Add(replacement);

            _adminLog.Add(LogType.EventStarted,
                LogImpact.Low,
                $"Replaced an unavailable pre-symptomatic emergent carrier with " +
                $"{ToPrettyString(replacement):host}");
        }

        if (state.Carriers.Count == 0)
            _emergentSeed = null;
    }

    private bool TryPickEmergentReplacement(
        EmergentSeedState state,
        out EntityUid replacement)
    {
        var candidates = _hosts.GetEligibleAutomaticHosts()
            .Where(candidate =>
                !state.Carriers.Any(carrier => carrier.Host == candidate) &&
                !_pathogen.IsInfected(candidate, state.Strain))
            .ToList();

        if (candidates.Count == 0)
        {
            replacement = default;
            return false;
        }

        var freshCandidates = candidates
            .Where(candidate => !_seededHosts.Contains(candidate))
            .ToList();

        replacement = _random.Pick(freshCandidates.Count > 0 ? freshCandidates : candidates);
        return true;
    }

    private bool HasReachedSymptomaticStage(EntityUid uid, int strain)
    {
        if (!TryComp<PathogenInfectionComponent>(uid, out var infections))
            return false;

        foreach (var infection in infections.Infections)
        {
            if (infection.Pathogen == strain && infection.Stage >= 1)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether this round contains someone who is going to release a virulent strain.
    ///
    /// The point of the reduction is to stop an emergent outbreak stacking on top of a
    /// bioterror one, since two serious strains at once is more than two virologists can
    /// work. It has to be answered up front rather than by checking for an active virulent
    /// strain, because contamination often crosses its milestone before the antagonist
    /// releases anything - by then it is too late to avoid the overlap.
    ///
    /// TODO: return true when a Plaguebearer is present, once that antagonist exists.
    /// Deliberately NOT "is there any antagonist" - a traitor stealing an ID has nothing to
    /// do with disease difficulty, and since nearly every round has one, that check would
    /// quietly suppress the entire emergent tier to a fraction of its intended rate.
    /// </summary>
    private bool HasBioterrorThreat()
    {
        return false;
    }

    private sealed class EmergentSeedState
    {
        public readonly int Strain;
        public readonly List<EmergentCarrier> Carriers;

        public EmergentSeedState(int strain, List<EntityUid> hosts)
        {
            Strain = strain;
            Carriers = hosts.Select(host => new EmergentCarrier(host)).ToList();
        }
    }

    private sealed class EmergentCarrier
    {
        public EntityUid Host;
        public bool Replaced;

        public EmergentCarrier(EntityUid host)
        {
            Host = host;
        }
    }
}
