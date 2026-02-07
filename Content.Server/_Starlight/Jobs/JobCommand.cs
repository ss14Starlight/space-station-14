using System.Linq;
using Content.Server.Administration;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Administration;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Jobs;

[AdminCommand(AdminFlags.Fun)]
[ToolshedCommand]
public sealed class JobCommand : ToolshedCommand
{
    private JobSystem? _job;
    private MindSystem? _mind;

    [CommandImplementation("set")]
    public EntityUid SetJob([PipedArgument] EntityUid uid, ProtoId<JobPrototype> job)
    {
        _job ??= EntitySystemManager.GetEntitySystem<JobSystem>();
        _mind ??= EntitySystemManager.GetEntitySystem<MindSystem>();
        if (!_mind.TryGetMind(uid, out var mind, out _)) return uid;
        _job.MindAddJob(mind, job);
        return uid;
    }

    [CommandImplementation("set")]
    public IEnumerable<EntityUid> SetJob([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<JobPrototype> job) =>
        uid.Select(x => SetJob(x, job));
}