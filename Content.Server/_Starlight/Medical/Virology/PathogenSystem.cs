using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Drives the lifecycle of every active infection: incubation, staging, symptom expression,
/// and recovery. A host carries at most one strain.
/// </summary>
public sealed partial class PathogenSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private PathogenHostSelectionSystem _hosts = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;

    /// <summary>
    /// Entities whose last infection cleared this tick. Collected and drained after the
    /// query, since removing a component mid-enumeration of that same component is not safe.
    /// </summary>
    private readonly List<EntityUid> _cleared = new();

    /// <summary>
    /// Strains that lost their last host this tick, for the respawn pass.
    /// </summary>
    private readonly List<int> _extinct = new();

    /// <summary>
    /// How often infections are advanced. Incubation, stages and symptoms are all authored
    /// in tens of seconds at the shortest, so re-evaluating them every tick only burns
    /// prototype lookups to arrive at the same answer.
    /// </summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenInfectionComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    /// <summary>
    /// Natural recovery is otherwise the only way into the respawn queue, and a host that
    /// dies never recovers. A carrier dying is not the crew running out of people who can
    /// catch this - the survivors still can - so the strain has to get its chance to
    /// reappear rather than silently leaving the round with the body.
    /// </summary>
    private void OnMobStateChanged(
        Entity<PathogenInfectionComponent> entity,
        ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        foreach (var infection in entity.Comp.Infections)
        {
            if (_registry.TryGetStrain(infection.Pathogen, out var strain))
                QueueExtinctionRespawn(strain);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Left outside the throttle: losing a last host is a discrete event rather than
        // something that has to be re-evaluated, and the pass costs nothing while the queue
        // is empty, which is nearly always.
        RespawnExtinctStrains();

        var curTime = _timing.CurTime;

        if (curTime < _nextTick)
            return;

        _nextTick = curTime + TickInterval;

        var mobStates = GetEntityQuery<MobStateComponent>();

        var query = EntityQueryEnumerator<PathogenInfectionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // The dead stop developing symptoms. They keep their infections, so a body
            // is still worth swabbing.
            if (mobStates.TryGetComponent(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
                continue;

            for (var i = comp.Infections.Count - 1; i >= 0; i--)
            {
                var infection = comp.Infections[i];

                if (!_registry.TryGetStrain(infection.Pathogen, out var strain))
                {
                    // Strain no longer exists - round restarted underneath us.
                    comp.Infections.RemoveAt(i);
                    continue;
                }

                if (infection.EndTime != TimeSpan.Zero && curTime >= infection.EndTime)
                {
                    comp.Infections.RemoveAt(i);

                    if (strain.ImmunityOnRecovery)
                        GrantImmunity(uid, strain.Id);

                    QueueExtinctionRespawn(strain);

                    continue;
                }

                if (infection.Stage < strain.MaxStage && curTime >= infection.NextStage)
                {
                    infection.Stage++;
                    infection.NextStage = curTime + strain.StageDelay;
                }

                // Still incubating.
                if (infection.Stage < 1)
                    continue;

                ExpressSymptoms(uid, infection, strain, curTime);
            }

            if (comp.Infections.Count == 0)
                _cleared.Add(uid);
        }

        foreach (var uid in _cleared)
            RemCompDeferred<PathogenInfectionComponent>(uid);

        _cleared.Clear();
    }

    /// <summary>
    /// An ambient strain that loses its last host reappears on someone else. The station
    /// does not get rid of a cold by waiting; it gets rid of it by running out of people
    /// who can still catch it, which the immunity check below handles on its own.
    /// </summary>
    private void RespawnExtinctStrains()
    {
        if (_extinct.Count == 0)
            return;

        if (!_config.GetCVar(StarlightCCVars.VirologyRespawnOnExtinction))
        {
            _extinct.Clear();
            return;
        }

        foreach (var strainId in _extinct)
        {
            if (AnyHostCarries(strainId))
                continue;

            if (!TryPickRandomHost(strainId, out var host))
                continue;

            TryInfect(host, strainId, cause: "ambient extinction respawn");
        }

        _extinct.Clear();
    }

    private void QueueExtinctionRespawn(Pathogen strain)
    {
        if (!strain.RespawnOnExtinction ||
            !_config.GetCVar(StarlightCCVars.VirologyRespawnOnExtinction))
        {
            return;
        }

        _extinct.Add(strain.Id);
    }

    private bool AnyHostCarries(int strainId)
    {
        var mobStates = GetEntityQuery<MobStateComponent>();
        var query = EntityQueryEnumerator<PathogenInfectionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // A strain sitting in a corpse is not in circulation. Counting it here would
            // let one body suppress the respawn forever, so the strain could neither
            // spread nor come back.
            if (mobStates.TryGetComponent(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
                continue;

            foreach (var infection in comp.Infections)
            {
                if (infection.Pathogen == strainId)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Picks a living, non-immune host at random. Returns false when the crew has
    /// collectively run out of people who can catch this, which is how a strain finally
    /// dies out for good.
    /// </summary>
    private bool TryPickRandomHost(int strainId, out EntityUid host)
    {
        var candidates = _hosts.GetEligibleAutomaticHosts()
            .Where(uid => !IsImmune(uid, strainId) && !IsInfected(uid, strainId))
            .ToList();

        if (candidates.Count == 0)
        {
            host = default;
            return false;
        }

        host = _random.Pick(candidates);
        return true;
    }

    private void ExpressSymptoms(EntityUid uid, PathogenInfection infection, Pathogen strain, TimeSpan curTime)
    {
        // The eligible count only feeds the stagger below, which only applies under an admin
        // interval override. Counting it otherwise is a second pass over every symptom
        // prototype for a number nothing reads.
        var eligibleSymptoms = 0;
        if (infection.SymptomIntervalOverride is not null)
        {
            foreach (var symptomId in strain.Symptoms)
            {
                if (_proto.TryIndex(symptomId, out var symptom) &&
                    infection.Stage >= symptom.MinStage)
                {
                    eligibleSymptoms++;
                }
            }
        }

        var eligibleIndex = 0;
        foreach (var symptomId in strain.Symptoms)
        {
            if (!_proto.TryIndex(symptomId, out var symptom))
                continue;

            if (infection.Stage < symptom.MinStage)
                continue;

            // First tick this symptom is eligible: schedule it instead of firing now, so
            // reaching a new stage doesn't dump every newly unlocked symptom at once.
            if (!infection.SymptomTimers.TryGetValue(symptomId, out var next))
            {
                var delay = NextInterval(symptom, infection);
                if (infection.SymptomIntervalOverride is { } interval && eligibleSymptoms > 1)
                    delay += interval * ((double) eligibleIndex / eligibleSymptoms);

                infection.SymptomTimers[symptomId] = curTime + delay;
                eligibleIndex++;
                continue;
            }

            eligibleIndex++;
            if (curTime < next)
                continue;

            infection.SymptomTimers[symptomId] = curTime + NextInterval(symptom, infection);

            foreach (var effect in symptom.Effects)
                _effects.TryApplyEffect(uid, effect, 1f);
        }
    }

    private TimeSpan NextInterval(PathogenSymptomPrototype symptom, PathogenInfection infection)
    {
        if (infection.SymptomIntervalOverride is { } interval)
            return interval;

        if (symptom.IntervalVariance <= 0f)
            return symptom.Interval;

        var factor = 1f + _random.NextFloat(-symptom.IntervalVariance, symptom.IntervalVariance);
        return symptom.Interval * Math.Max(0.1f, factor);
    }

    public bool TryExpressSymptom(
        EntityUid uid,
        ProtoId<PathogenSymptomPrototype> symptomId,
        out PathogenSymptomPrototype? symptom)
    {
        if (!_proto.TryIndex(symptomId, out symptom))
            return false;

        foreach (var effect in symptom.Effects)
            _effects.TryApplyEffect(uid, effect, 1f);

        return true;
    }

    /// <summary>
    /// Infects an entity unless it is immune or invalid. A higher-tier strain displaces
    /// the current strain and grants immunity to the displaced strain; an equal or
    /// lower-tier strain bounces off.
    /// </summary>
    public bool TryInfect(EntityUid uid, int strainId, bool bypassImmunity = false, string cause = "unknown")
    {
        if (!_registry.TryGetStrain(strainId, out var strain))
            return false;

        if (!CanHost(uid))
            return false;

        if (!bypassImmunity && IsImmune(uid, strainId))
            return false;

        var comp = EnsureComp<PathogenInfectionComponent>(uid);

        if (comp.Infections.Count > 0)
        {
            var existing = comp.Infections[0];
            if (existing.Pathogen == strainId)
                return false;

            if (!_registry.TryGetStrain(existing.Pathogen, out var existingStrain))
            {
                comp.Infections.Clear();
            }
            else
            {
                if (strain.Tier <= existingStrain.Tier)
                    return false;

                GrantImmunity(uid, existingStrain.Id);
                comp.Infections.Clear();
            }
        }

        var curTime = _timing.CurTime;

        comp.Infections.Add(new PathogenInfection
        {
            Pathogen = strainId,
            Stage = 0,
            NextStage = curTime + strain.Incubation,
            EndTime = strain.Duration == TimeSpan.Zero
                ? TimeSpan.Zero
                : curTime + strain.Incubation + strain.Duration,
        });

        // Logged here rather than at each call site so every route - natural spread, a
        // contamination source, an admin verb - lands one entry, and the cause is what
        // separates an outbreak running its course from someone spreading it deliberately.
        _adminLog.Add(LogType.Virology, LogImpact.Low,
            $"{ToPrettyString(uid):host} was infected with {strain.Designation:strain} ({strain.Tier}/{strain.PathogenType}) via {cause}");

        return true;
    }

    /// <summary>
    /// Clears one strain from a host. Unlike natural recovery this does not grant immunity
    /// by default, so curing someone does not also vaccinate them.
    /// </summary>
    public bool Cure(EntityUid uid, int strainId, bool grantImmunity = false, string cause = "unknown")
    {
        if (!TryComp<PathogenInfectionComponent>(uid, out var comp))
            return false;

        if (comp.Infections.RemoveAll(x => x.Pathogen == strainId) == 0)
            return false;

        if (grantImmunity)
            GrantImmunity(uid, strainId);

        var designation = _registry.TryGetStrain(strainId, out var strain)
            ? strain.Designation
            : $"#{strainId}";

        // One interpolated literal, no concatenation: the overload takes a LogStringHandler,
        // and a '+' would collapse it to a plain string and lose the structured fields.
        var immunity = grantImmunity ? " and became immune" : string.Empty;

        _adminLog.Add(LogType.Virology, LogImpact.Low,
            $"{ToPrettyString(uid):host} was cured of {designation:strain} via {cause}{immunity}");

        if (comp.Infections.Count == 0)
            RemCompDeferred<PathogenInfectionComponent>(uid);

        return true;
    }

    /// <summary>
    /// Moves an existing infection to a requested stage and restarts its stage and symptom
    /// timers. Used by admin testing tools so passive progression can be checked without
    /// waiting through incubation.
    /// </summary>
    public bool TrySetStage(EntityUid uid, int strainId, int stage, out int actualStage)
    {
        actualStage = 0;

        if (!_registry.TryGetStrain(strainId, out var strain) ||
            !TryComp<PathogenInfectionComponent>(uid, out var comp))
        {
            return false;
        }

        foreach (var infection in comp.Infections)
        {
            if (infection.Pathogen != strainId)
                continue;

            actualStage = Math.Clamp(stage, 0, strain.MaxStage);
            infection.Stage = actualStage;
            infection.NextStage = _timing.CurTime + strain.StageDelay;
            infection.SymptomTimers.Clear();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Overrides symptom cadence for an existing infection. This is intended for admin
    /// live testing; null restores the authored symptom intervals.
    /// </summary>
    public bool TrySetSymptomInterval(EntityUid uid, int strainId, TimeSpan? interval)
    {
        if (!TryComp<PathogenInfectionComponent>(uid, out var comp))
            return false;

        foreach (var infection in comp.Infections)
        {
            if (infection.Pathogen != strainId)
                continue;

            infection.SymptomIntervalOverride = interval;
            infection.SymptomTimers.Clear();
            return true;
        }

        return false;
    }

    public bool IsInfected(EntityUid uid, int strainId)
    {
        if (!TryComp<PathogenInfectionComponent>(uid, out var comp))
            return false;

        foreach (var infection in comp.Infections)
        {
            if (infection.Pathogen == strainId)
                return true;
        }

        return false;
    }

    public bool IsImmune(EntityUid uid, int strainId)
    {
        if (!TryComp<PathogenImmunityComponent>(uid, out var comp))
            return false;

        return comp.Total || comp.Immune.Contains(strainId);
    }

    public void GrantImmunity(EntityUid uid, int strainId)
    {
        var comp = EnsureComp<PathogenImmunityComponent>(uid);
        comp.Immune.Add(strainId);
    }

    /// <summary>
    /// Whether this entity is something a pathogen can live in at all.
    /// Requires a living mob; silicons and anything else unsuitable opt out with
    /// <see cref="PathogenImmunityComponent.Total"/>.
    /// </summary>
    public bool CanHost(EntityUid uid)
        => _hosts.CanHost(uid);
}
