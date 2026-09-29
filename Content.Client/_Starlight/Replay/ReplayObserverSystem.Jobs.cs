using Content.Shared.Access.Systems;
using Content.Shared.Roles.Jobs;

namespace Content.Client._Starlight.Replay;

// The mind's job is only networked in recordings made with MindRoleJobNetworkingSystem; older ones fall back to the ID.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    public bool TryGetReplayJobName(EntityUid entity, EntityUid? mindId, out string job)
    {
        if (_jobs.MindTryGetJob(mindId, out var proto))
        {
            job = proto.LocalizedName;
            return true;
        }

        if (_idCard.TryFindIdCard(entity, out var id) && id.Comp.LocalizedJobTitle is { Length: > 0 } title)
        {
            job = Loc.GetString("replay-observer-job-from-id", ("job", title));
            return true;
        }

        job = string.Empty;
        return false;
    }
}
