using System.Linq;
using Content.Server.Administration;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Jobs;

[AdminCommand(AdminFlags.Fun)]
[ToolshedCommand]
public sealed class JobCommand : ToolshedCommand
{
    private JobSystem? _job;
    private MindSystem? _mind;
    private RoleSystem? _roles;

    [CommandImplementation("set")]
    public EntityUid SetJob([PipedArgument] EntityUid uid, ProtoId<JobPrototype> job)
    {
        _job ??= GetSys<JobSystem>();
        _mind ??= GetSys<MindSystem>();
        if (!_mind.TryGetMind(uid, out var mind, out _)) return uid;
        _job.MindAddJob(mind, job);
        return uid;
    }

    [CommandImplementation("set")]
    public IEnumerable<EntityUid> SetJob([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<JobPrototype> job) =>
        uid.Select(x => SetJob(x, job));

    [CommandImplementation("delset")]
    public EntityUid DelSetJob([PipedArgument] EntityUid uid, ProtoId<JobPrototype> job)
    {
        _mind ??= GetSys<MindSystem>();
        _roles ??= GetSys<RoleSystem>();
        if (!_mind.TryGetMind(uid, out var mind, out _)) return uid;
        _roles.MindRemoveRole<JobRoleComponent>(mind);
        return SetJob(uid, job);
    }

    [CommandImplementation("delset")]
    public IEnumerable<EntityUid> DelSetJob([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<JobPrototype> job)
        => uid.Select(x => DelSetJob(x, job));

    [CommandImplementation("dobriefing")]
    public EntityUid DoBriefing(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool skipRole)
    {
        _mind ??= GetSys<MindSystem>();
        _job ??= GetSys<JobSystem>();
        if (!_mind.TryGetMind(uid, out var mindId, out var mind)) return uid;
        _job.MindOnDoGreeting(mindId, mind, skipRole);
        return uid;
    }

    [CommandImplementation("dobriefing")]
    public IEnumerable<EntityUid> DoBriefing(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        bool skipRole) =>
        uid.Select(x => DoBriefing(ctx, x, skipRole));

    // Technically there should prob be a role command instead of this being here, but I will prob do that if I ever refactor role/job system to be less stupid.
    [CommandImplementation("setrole")]
    public EntityUid SetRole(IInvocationContext ctx, [PipedArgument] EntityUid uid, ProtoId<RoleTypePrototype> role, bool notifyRoleUpdate)
    {
        _mind ??= GetSys<MindSystem>();
        _roles ??= GetSys<RoleSystem>();
        if (!_mind.TryGetMind(uid, out var mindId, out var mind)) return uid;
        _roles.SetRoleType(mindId, role, null);
        if (notifyRoleUpdate) _roles.RoleUpdateMessage(mind);
        return uid;
    }

    [CommandImplementation("setrole")]
    public IEnumerable<EntityUid> SetRole(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        ProtoId<RoleTypePrototype> role, bool notifyRoleUpdate) =>
        uid.Select(x => SetRole(ctx, x, role, notifyRoleUpdate));

    [CommandImplementation("doroleupdate")]
    public EntityUid DoRoleUpdate(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _mind ??= GetSys<MindSystem>();
        _roles ??= GetSys<RoleSystem>();
        if (!_mind.TryGetMind(uid, out _, out var mind)) return uid;
        _roles.RoleUpdateMessage(mind);
        return uid;
    }

    [CommandImplementation("doroleupdate")]
    public IEnumerable<EntityUid> DoRoleUpdate(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => DoRoleUpdate(ctx, x));
}
