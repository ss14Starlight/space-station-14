using Content.Client.Replay.Spectator;
using Content.Shared.Follower.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles.Jobs;
using Content.Shared.Tag;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Replay;

public sealed partial class ReplayObserverSystem
{
    private static readonly EntProtoId _adminObserverProto = "AdminObserver";
    private static readonly ProtoId<TagPrototype> _notGhostnadoWarpableTag = "NotGhostnadoWarpable";

    [Dependency] private TagSystem _tags = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private ReplaySpectatorSystem _spectator = default!;

    public List<GhostWarp> GetReplayWarps()
    {
        var warps = new List<GhostWarp>();
        var local = _player.LocalEntity;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } ent
                || ent == local
                || !Exists(ent)
                || IsClientSide(ent)
                || HasComp<GhostComponent>(ent))
            {
                continue;
            }

            if (!_mobState.IsAlive(ent) && !_mobState.IsCritical(ent))
                continue;

            var name = Name(ent);

            if (_mind.TryGetMind(ent, out var mindId, out _) && _jobs.MindTryGetJobName(mindId, out var job))
                name = $"{name} ({job})";

            warps.Add(new GhostWarp(GetNetEntity(ent), name, false));
        }

        var query = AllEntityQuery<WarpPointComponent>();
        while (query.MoveNext(out var uid, out var warp))
        {
            warps.Add(new GhostWarp(GetNetEntity(uid), warp.Location ?? Name(uid), true));
        }

        return warps;
    }

    public void WarpTo(NetEntity target)
    {
        var uid = GetEntity(target);
        if (!Exists(uid))
            return;

        if (_player.LocalEntity is { } observer && observer == _observer && Exists(observer))
        {
            // Parenting first handles targets inside containers.
            _transform.SetCoordinates(observer, new EntityCoordinates(uid, default));
            _transform.AttachToGridOrMap(observer);
            return;
        }

        _spectator.SpawnSpectatorGhost(new EntityCoordinates(uid, default), true);
    }

    /// <summary>
    /// Same pick as FollowerSystem.GetMostGhostFollowed. Warps only, since following would edit the target's recorded
    /// FollowedComponent. Admin status isn't recorded, so aghosts are excluded by prototype.
    /// Shows a flyover text when no eligible followed target exists as a fail state.
    /// </summary>
    public void WarpToMostFollowed()
    {
        var counts = new Dictionary<EntityUid, int>();
        var query = AllEntityQuery<FollowerComponent, GhostComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var follower, out _, out var meta))
        {
            if (uid == _observer
                || meta.EntityPrototype?.ID == _adminObserverProto.Id
                || !_player.TryGetSessionByEntity(uid, out _))
            {
                continue;
            }

            var followed = follower.Following;
            if (!Exists(followed) || _tags.HasTag(followed, _notGhostnadoWarpableTag))
                continue;

            counts.TryGetValue(followed, out var count);
            counts[followed] = count + 1;
        }

        EntityUid? target = null;
        var most = 0;
        foreach (var (followed, count) in counts)
        {
            if (count <= most)
                continue;

            most = count;
            target = followed;
        }

        if (target == null)
        {
            if (_player.LocalEntity is { } local)
                _popup.PopupEntity(Loc.GetString("replay-observer-ghostnado-none"), local);

            return;
        }

        WarpTo(GetNetEntity(target.Value));
    }
}
