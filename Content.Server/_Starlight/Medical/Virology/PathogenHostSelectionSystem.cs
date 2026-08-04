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
/// Central eligibility rules for automatic pathogen seeds. Natural transmission uses
/// biological host eligibility only, so going AFK cannot grant disease immunity.
/// </summary>
public sealed partial class PathogenHostSelectionSystem : EntitySystem
{
    [Dependency] private IAfkManager _afk = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private PathogenIsolationSystem _isolation = default!;

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

    public bool CanHost(EntityUid uid)
    {
        if (!TryComp<MobStateComponent>(uid, out var mobState) ||
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
               !_isolation.IsIsolated(uid) &&
               TryComp<MindContainerComponent>(uid, out var mind) &&
               mind.HasMind &&
               CanHost(uid);
    }
}
