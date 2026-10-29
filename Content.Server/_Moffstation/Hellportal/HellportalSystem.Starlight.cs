using Content.Server._Moffstation.Hellportal.Components;
using Robust.Server.Player;

namespace Content.Server._Moffstation.Hellportal;

public sealed partial class HellportalSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly Dictionary<EntityUid, int> _mobCounts = new();

    private void CountSpawnedMobs()
    {
        _mobCounts.Clear();
        var query = AllEntityQuery<HellportalMobComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SourcePortal is not { } portal || TerminatingOrDeleted(uid))
                continue;

            _mobCounts[portal] = _mobCounts.GetValueOrDefault(portal) + 1;
        }
    }
}
