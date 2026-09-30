using Content.Shared.Access.Systems;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;

namespace Content.Client._Starlight.Replay;

// The mind's job is only networked in recordings made with MindRoleJobNetworkingSystem. Older recordings fall back to
// the carried ID; newer ones never do, so each replay has a single source of truth.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private bool _replayHasMindJobs;

    private void UpdateMindJobPresence()
    {
        if (_replayHasMindJobs)
            return;

        var query = AllEntityQuery<JobRoleComponent, MindRoleComponent>();
        while (query.MoveNext(out _, out _, out var role))
        {
            if (role.JobPrototype == null)
                continue;

            _replayHasMindJobs = true;
            return;
        }
    }

    public bool TryGetReplayJobName(EntityUid entity, EntityUid? mindId, out string job)
    {
        if (_jobs.MindTryGetJob(mindId, out var proto))
        {
            job = proto.LocalizedName;
            return true;
        }

        if (!_replayHasMindJobs && _idCard.TryFindIdCard(entity, out var id) && id.Comp.LocalizedJobTitle is { Length: > 0 } title)
        {
            job = Loc.GetString("replay-observer-job-from-id", ("job", title));
            return true;
        }

        job = string.Empty;
        return false;
    }
}
