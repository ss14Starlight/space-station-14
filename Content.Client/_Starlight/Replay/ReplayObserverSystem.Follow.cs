using System.Linq;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.GameStates;

namespace Content.Client._Starlight.Replay;

// A followed entity's recorded follower list doesn't include the observer, so when the recording deletes it the
// observer is deleted along with it and respawns at the map origin. Detach first. A station AI swaps its eye entity
// whenever it toggles remote mode (e.g. holopads); follow the replacement as the live server does.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private FollowerSystem _follower = default!;

    private Entity<StationAiCoreComponent>? _refollowCore;
    private EntityUid? _refollowOldEye;

    private void OnBeforeApplyStateFollow((GameState Current, GameState? Next) args)
    {
        if (!IsViewing()
            || _observer is not { } observer
            || !TryComp<FollowerComponent>(observer, out var follower))
        {
            return;
        }

        var followed = follower.Following;
        var deletions = args.Current.EntityDeletions;
        if (!deletions.HasContents || !deletions.Value.Contains(GetNetEntity(followed)))
            return;

        var cores = AllEntityQuery<StationAiCoreComponent>();
        while (cores.MoveNext(out var core, out var coreComp))
        {
            if (coreComp.RemoteEntity != followed)
                continue;

            _refollowCore = (core, coreComp);
            _refollowOldEye = followed;
            break;
        }

        _follower.StopFollowingEntity(observer, followed);

        if (Transform(observer).ParentUid != followed)
            return;

        RemComp<FollowerComponent>(observer);
        RemComp<OrbitVisualsComponent>(observer);
        _transform.AttachToGridOrMap(observer);
    }

    private void UpdateRefollow()
    {
        if (_refollowCore is not { } core)
            return;

        if (!Exists(core))
        {
            _refollowCore = null;
            return;
        }

        if (!IsViewing()
            || _observer is not { } observer
            || core.Comp.RemoteEntity is not { } eye
            || eye == _refollowOldEye
            || !Exists(eye))
        {
            return;
        }

        _refollowCore = null;
        _follower.StartFollowingEntity(observer, eye);
    }
}
