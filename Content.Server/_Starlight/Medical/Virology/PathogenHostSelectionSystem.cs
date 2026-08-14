using Content.Server.Afk;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Central eligibility rules for automatic pathogen hosts. Future natural transmission will use
/// biological host eligibility only, so going AFK cannot grant disease immunity.
/// </summary>
public sealed partial class PathogenHostSelectionSystem : EntitySystem
{
    [Dependency] private IAfkManager _afk = default!;
    [Dependency] private IPlayerManager _players = default!;

    public List<EntityUid> GetEligibleAutomaticHosts()
    {
        var hosts = new List<EntityUid>();

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } uid ||
                !IsEligibleAutomaticHost(session, uid))
            {
                continue;
            }

            hosts.Add(uid);
        }

        return hosts;
    }

    public bool IsEligibleAutomaticHost(EntityUid uid)
    {
        return _players.TryGetSessionByEntity(uid, out var session) &&
            IsEligibleAutomaticHost(session, uid);
    }

    /// <summary>
    /// Every living humanoid, played or not. This is the denominator a strain's prevalence
    /// is measured against, so it deliberately counts more than
    /// <see cref="GetEligibleAutomaticHosts"/> - an SSD crewman still occupies a share of
    /// the outbreak.
    /// </summary>
    public int CountLivingCrew()
    {
        var crew = 0;

        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, MobStateComponent>();
        while (query.MoveNext(out _, out _, out var mobState))
        {
            if (mobState.CurrentState != MobState.Dead)
                crew++;
        }

        return crew;
    }

    /// <summary>
    /// Whether a pathogen can live in this entity at all.
    ///
    /// Crew only: HumanoidAppearance separates playable species from animals and NPCs. This
    /// also keeps the prevalence cap consistent, since the cap is measured against living
    /// crew and an infected animal would spend budget without counting toward it.
    ///
    /// Humanoids that should never be infected - synthetics, the undead - opt out with
    /// <see cref="PathogenImmunityComponent.Total"/>.
    /// </summary>
    public bool CanHost(EntityUid uid)
    {
        if (!HasComp<HumanoidAppearanceComponent>(uid) ||
            !TryComp<MobStateComponent>(uid, out var mobState) ||
            mobState.CurrentState == MobState.Dead)
        {
            return false;
        }

        return !TryComp<PathogenImmunityComponent>(uid, out var immunity) || !immunity.Total;
    }

    private bool IsEligibleAutomaticHost(ICommonSession session, EntityUid uid)
    {
        return session.Status == SessionStatus.InGame &&
            !_afk.IsAfk(session) &&
            Exists(uid) &&
            !TerminatingOrDeleted(uid) &&
            HasComp<HumanoidAppearanceComponent>(uid) &&
            !HasComp<CryostorageContainedComponent>(uid) &&
            TryComp<MindContainerComponent>(uid, out var mind) &&
            mind.HasMind &&
            CanHost(uid);
    }
}
