using System.Linq;
using System.Runtime.InteropServices;
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

namespace Content.Server._Starlight.Roles;

[AdminCommand(AdminFlags.Fun)]
[ToolshedCommand]
public sealed partial class RoleCommand : ToolshedCommand
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _factory = default!;
    private JobSystem? _job;
    private MindSystem? _mind;
    private RoleSystem? _roles;

    [CommandImplementation("setjob")]
    public EntityUid SetJob(IInvocationContext ctx, [PipedArgument] EntityUid uid, ProtoId<JobPrototype> job)
    {
        _job ??= GetSys<JobSystem>();
        _mind ??= GetSys<MindSystem>();
        if (!_mind.TryGetMind(uid, out var mind, out _)) return uid;
        _job.MindAddJob(mind, job);
        ctx.WriteLine($"Set {EntityManager.ToPrettyString(uid)}'s job to {job.Id}.");
        return uid;
    }

    [CommandImplementation("setjob")]
    public IEnumerable<EntityUid> SetJob(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, ProtoId<JobPrototype> job) =>
        uid.Select(x => SetJob(ctx, x, job));

    [CommandImplementation("rmjob")]
    public EntityUid RemoveJob(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _mind ??= GetSys<MindSystem>();
        _roles ??= GetSys<RoleSystem>();
        if (!_mind.TryGetMind(uid, out var mind, out _)) return uid;
        _roles.MindRemoveRole<JobRoleComponent>(mind);
        ctx.WriteLine($"Removed job from {EntityManager.ToPrettyString(uid)}.");
        return uid;
    }

    [CommandImplementation("rmjob")]
    public IEnumerable<EntityUid> RemoveJob(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x => RemoveJob(ctx, x));

    [CommandImplementation("rmsetjob")]
    public EntityUid RemoveThenSetJob(IInvocationContext ctx, [PipedArgument] EntityUid uid, ProtoId<JobPrototype> job)
    {
        RemoveJob(ctx, uid);
        return SetJob(ctx, uid, job);
    }

    [CommandImplementation("rmsetjob")]
    public IEnumerable<EntityUid> RemoveThenSetJob(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        ProtoId<JobPrototype> job)
        => uid.Select(x => RemoveThenSetJob(ctx, x, job));

    [CommandImplementation("dobriefing")]
    public EntityUid DoBriefing(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool skipRole)
    {
        _mind ??= GetSys<MindSystem>();
        _job ??= GetSys<JobSystem>();
        if (!_mind.TryGetMind(uid, out var mindId, out var mind)) return uid;
        _job.MindOnDoGreeting(mindId, mind, skipRole);
        ctx.WriteLine($"Showed briefing to {EntityManager.ToPrettyString(uid)}");
        return uid;
    }

    [CommandImplementation("dobriefing")]
    public IEnumerable<EntityUid> DoBriefing(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        bool skipRole) =>
        uid.Select(x => DoBriefing(ctx, x, skipRole));

    // Technically there should prob be a role command instead of this being here, but I will prob do that if I ever refactor role/job system to be less stupid.
    [CommandImplementation("setroletype")]
    public EntityUid SetRole(IInvocationContext ctx, [PipedArgument] EntityUid uid, ProtoId<RoleTypePrototype> role,
        bool notifyRoleUpdate)
    {
        _mind ??= GetSys<MindSystem>();
        _roles ??= GetSys<RoleSystem>();
        if (!_mind.TryGetMind(uid, out var mindId, out var mind)) return uid;
        _roles.SetRoleType(mindId, role, null);
        if (notifyRoleUpdate) _roles.RoleUpdateMessage(mind);
        ctx.WriteLine($"Set {EntityManager.ToPrettyString(uid)}'s role to {role.Id}.");
        return uid;
    }

    [CommandImplementation("setroletype")]
    public IEnumerable<EntityUid> SetRole(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        ProtoId<RoleTypePrototype> role, bool notifyRoleUpdate) =>
        uid.Select(x => SetRole(ctx, x, role, notifyRoleUpdate));

    // TODO: When my RT pull request gets merged, replace [DefaultParameterValue] in favor of OptionalValue<T>.
    [CommandImplementation("addrole")]
    public EntityUid AddRole(IInvocationContext ctx, [PipedArgument] EntityUid uid, MindRoleProtoId mindRolePrototype,
        [Optional] [DefaultParameterValue(true)] bool silent)
    {
        if (!_proto.TryIndex(mindRolePrototype.ProtoId, out var proto))
        {
            ctx.WriteMarkup($"[color=red]Invalid prototype id: {mindRolePrototype.ProtoId.Id}[/color]");
            return uid;
        }

        if (!proto.TryGetComponent(out MindRoleComponent? _, _factory))
        {
            ctx.WriteMarkup($"[color=red]Prototype ID {proto.ID} is not a mind role.[/color]");
            return uid;
        }

        if (proto.ID == "MindRoleJob")
        {
            ctx.WriteMarkup(
                $"[color=red]Prototype ID {proto.ID} is for job roles and does nothing. Don't use this, use [color=magenta]role:setjob[/color] to set job role.[/color]");
            return uid;
        }

        _mind ??= GetSys<MindSystem>();
        _roles ??= GetSys<RoleSystem>();
        if (!_mind.TryGetMind(uid, out var mindUid, out var mind)) return uid;
        _roles.MindAddRole(mindUid, mindRolePrototype.ProtoId, mind, silent);
        ctx.WriteLine($"Added role {proto.ID} to entity {EntityManager.ToPrettyString(uid)}.");
        return uid;
    }

    [CommandImplementation("addrole")]
    public IEnumerable<EntityUid> AddRole(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        MindRoleProtoId mindRolePrototype, [Optional] [DefaultParameterValue(true)] bool silent) =>
        uid.Select(x => AddRole(ctx, x, mindRolePrototype, silent));

    [CommandImplementation("rmrole")]
    public EntityUid RemoveRole(IInvocationContext ctx, [PipedArgument] EntityUid uid, MindRoleEntity mindRole)
    {
        _mind ??= GetSys<MindSystem>();
        _roles ??= GetSys<RoleSystem>();
        if (!_mind.TryGetMind(uid, out var mindUid, out var mind)) return uid;
        _roles.MindRemoveRole((mindUid, mind),
            new EntProtoId<MindRoleComponent>(MetaData(mindRole.Entity).EntityPrototype!.ID));
        ctx.WriteLine($"Removed role {mindRole.Entity} from {EntityManager.ToPrettyString(uid)}.");
        return uid;
    }

    [CommandImplementation("rmrole")]
    public IEnumerable<EntityUid> RemoveRole(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, MindRoleEntity mindRole) =>
        uid.Select(x => RemoveRole(ctx, x, mindRole));

    [CommandImplementation("doroleupdate")]
    public EntityUid DoRoleUpdate(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _mind ??= GetSys<MindSystem>();
        _roles ??= GetSys<RoleSystem>();
        if (!_mind.TryGetMind(uid, out _, out var mind)) return uid;
        _roles.RoleUpdateMessage(mind);
        ctx.WriteLine($"Showed role update message to {EntityManager.ToPrettyString(uid)}.");
        return uid;
    }

    [CommandImplementation("doroleupdate")]
    public IEnumerable<EntityUid> DoRoleUpdate(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => DoRoleUpdate(ctx, x));
}
