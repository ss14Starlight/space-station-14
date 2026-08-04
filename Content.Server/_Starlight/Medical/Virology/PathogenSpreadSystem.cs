using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Interaction;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Handles virus inhalation spread between nearby hosts. Bacteria is handled by completed
/// contact actions, and fungus is handled exclusively by environmental sources.
/// </summary>
public sealed partial class PathogenSpreadSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private PathogenTransmissionSystem _transmission = default!;

    /// <summary>
    /// How often transmission is tested. Deliberately coarse: this is an
    /// O(hosts * neighbours) sweep and nothing about disease needs sub-second fidelity.
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    private TimeSpan _nextSweep;

    private readonly HashSet<EntityUid> _nearby = new();
    private readonly List<(EntityUid Source, Pathogen Strain)> _shedding = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime < _nextSweep)
            return;

        _nextSweep = curTime + SweepInterval;
        Sweep();
    }

    /// <summary>
    /// Runs the normal virus proximity sweep immediately. This preserves authored chance,
    /// PPE, prevalence, immunity, and line-of-sight checks.
    /// </summary>
    public void SweepNow()
        => Sweep();

    private void Sweep()
    {
        _shedding.Clear();
        var query = EntityQueryEnumerator<PathogenInfectionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var infection in comp.Infections)
            {
                if (!_registry.TryGetStrain(infection.Pathogen, out var strain))
                    continue;

                if (strain.Transmissibility <= 0f)
                    continue;

                // Only viruses spread person-to-person through proximity. Bacteria requires
                // physical contact, while fungus is exclusively environmental.
                if (strain.PathogenType != PathogenType.Virus ||
                    _transmission.AtCap(strain))
                    continue;

                _shedding.Add((uid, strain));
            }
        }

        var xforms = GetEntityQuery<TransformComponent>();
        foreach (var (source, strain) in _shedding)
        {
            if (_transmission.AtCap(strain) ||
                !xforms.TryGetComponent(source, out var xform))
                continue;

            TrySpread(source, xform, strain);
        }

        _shedding.Clear();
    }

    /// <summary>
    /// One transmission per host per sweep at most. Without this a carrier in a crowded
    /// bar rolls against everyone in the room every few seconds, and a strain crosses the
    /// station in a minute regardless of how low its chance is set.
    /// </summary>
    private bool TrySpread(EntityUid source, TransformComponent xform, Pathogen strain)
    {
        var range = strain.SpreadRange * 1.5f;
        var chance = strain.Transmissibility;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, range, _nearby, LookupFlags.Uncontained);

        foreach (var target in _nearby)
        {
            if (target == source)
                continue;

            if (!_interaction.InRangeUnobstructed(source, target, range, overlapCheck: false))
                continue;

            if (_transmission.TryExpose(target, strain, chance))
                return true;
        }

        return false;
    }

}
