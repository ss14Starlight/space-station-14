using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Applies the common eligibility, prevalence, resistance, and infection rules for every
/// natural transmission route.
/// </summary>
public sealed partial class PathogenTransmissionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private PathogenResistanceSystem _resistance = default!;
    [Dependency] private PathogenSystem _pathogen = default!;

    private readonly Dictionary<int, int> _hostCounts = new();
    private readonly Dictionary<PathogenTier, int> _tierCounts = new();

    private GameTick _countTick;
    private bool _countsValid;
    private int _livingCrew;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _countsValid = false);
    }

    public bool TryExpose(EntityUid target, Pathogen strain, float chance)
    {
        if (chance <= 0f || !CanExpose(target, strain))
        {
            return false;
        }

        var targetChance = Math.Clamp(chance, 0f, 1f) *
                           _resistance.GetResistance(target, strain);

        if (!_random.Prob(targetChance) ||
            !_pathogen.TryInfect(target, strain.Id))
        {
            return false;
        }

        // Infection may have displaced a lower-tier strain, so rebuild both strain and
        // tier counts before the next cap check instead of applying one-sided increments.
        _countsValid = false;
        return true;
    }

    public bool CanExpose(EntityUid target, Pathogen strain)
    {
        return !AtCap(strain) &&
               _pathogen.CanHost(target) &&
               !_pathogen.IsInfected(target, strain.Id) &&
               !_pathogen.IsImmune(target, strain.Id);
    }

    public bool AtCap(Pathogen strain)
    {
        RefreshCounts();

        if (_livingCrew == 0 ||
            _hostCounts.GetValueOrDefault(strain.Id) >= _livingCrew * strain.MaxPrevalence)
        {
            return true;
        }

        var tierCap = TierCap(strain.Tier);
        return !float.IsPositiveInfinity(tierCap) &&
               _tierCounts.GetValueOrDefault(strain.Tier) >= _livingCrew * tierCap;
    }

    private void RefreshCounts()
    {
        if (_countsValid && _countTick == _timing.CurTick)
            return;

        _countsValid = true;
        _countTick = _timing.CurTick;
        _livingCrew = 0;
        _hostCounts.Clear();
        _tierCounts.Clear();

        var crewQuery = EntityQueryEnumerator<HumanoidAppearanceComponent, MobStateComponent>();
        while (crewQuery.MoveNext(out _, out _, out var mobState))
        {
            if (mobState.CurrentState != MobState.Dead)
                _livingCrew++;
        }

        var infectionQuery = EntityQueryEnumerator<PathogenInfectionComponent>();
        while (infectionQuery.MoveNext(out _, out var infections))
        {
            foreach (var infection in infections.Infections)
            {
                _hostCounts[infection.Pathogen] =
                    _hostCounts.GetValueOrDefault(infection.Pathogen) + 1;

                if (_registry.TryGetStrain(infection.Pathogen, out var strain))
                {
                    _tierCounts[strain.Tier] =
                        _tierCounts.GetValueOrDefault(strain.Tier) + 1;
                }
            }
        }
    }

    private static float TierCap(PathogenTier tier) => tier switch
    {
        PathogenTier.Ambient => 0.15f,
        PathogenTier.Emergent => 0.15f,
        _ => float.PositiveInfinity,
    };
}
