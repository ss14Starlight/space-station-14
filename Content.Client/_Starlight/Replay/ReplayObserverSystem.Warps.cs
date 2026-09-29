using System.Linq;
using Content.Client.Replay.Spectator;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Client._Starlight.Replay;

public sealed partial class ReplayObserverSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMindSystem _mind = default!;
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

            EntityUid? mindId = _mind.TryGetMind(ent, out var foundMind, out _) ? foundMind : null;
            if (TryGetReplayJobName(ent, mindId, out var job))
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

    public void WarpToRandomPlayer()
    {
        var players = GetReplayWarps().Where(w => !w.IsWarpPoint).ToList();
        if (players.Count == 0)
            return;

        WarpTo(_random.Pick(players).Entity);
    }
}
