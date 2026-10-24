using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Admin)]
public sealed class MarkCommand : ToolshedCommand
{
    [CommandImplementation("set")]
    public EntityUid Mark(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        var marked = new List<EntityUid> { uid };
        ctx.WriteVar("marked", marked);
        return uid;
    }

    [CommandImplementation("set")]
    public List<EntityUid> Mark(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uids)
    {
        var marked = uids.ToList();
        ctx.WriteVar("marked", marked);
        return marked;
    }

    [CommandImplementation("append")]
    public EntityUid JoinMarked(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        var obj = ctx.ReadVar("marked");
        if (obj is not List<EntityUid> marked) return Mark(ctx, uid);
        if (!marked.Contains(uid))
            marked.Add(uid);
        ctx.WriteVar("marked", marked);
        return uid;
    }

    [CommandImplementation("append")]
    public List<EntityUid> JoinMarked(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uids)
    {
        var list = uids.ToList();
        foreach (var uid in list) JoinMarked(ctx, uid);
        return list;
    }

    [CommandImplementation("appendget")]
    public List<EntityUid> JoinMarkedGet(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        var obj = ctx.ReadVar("marked");
        if (obj is not List<EntityUid> marked) return [Mark(ctx, uid)];
        var set = marked.ToHashSet();
        set.Add(uid);
        ctx.WriteVar("marked", set.ToList());
        return [.. set];
    }

    [CommandImplementation("appendget")]
    public List<EntityUid> JoinMarkedGet(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uids)
    {
        var marked = uids.ToList();
        foreach (var uid in marked) JoinMarkedGet(ctx, uid);
        return marked;
    }

    [CommandImplementation("rm")]
    public EntityUid RemoveMarked(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        var obj = ctx.ReadVar("marked");
        if (obj is not List<EntityUid> marked) return Mark(ctx, uid);
        marked.Remove(uid);
        ctx.WriteVar("marked", marked);
        return uid;
    }

    [CommandImplementation("rm")]
    public List<EntityUid> RemoveMarked(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uids)
    {
        var list = uids.ToList();
        foreach (var uid in list) RemoveMarked(ctx, uid);
        return list;
    }

    [CommandImplementation("rmget")]
    public List<EntityUid> RemoveMarkedGet(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        var obj = ctx.ReadVar("marked");
        if (obj is not List<EntityUid> marked) return [Mark(ctx, uid)];
        marked.Remove(uid);
        ctx.WriteVar("marked", marked);
        return marked;
    }

    [CommandImplementation("rmget")]
    public List<EntityUid> RemoveMarkedGet(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uids)
    {
        var list = uids.ToList();
        foreach (var uid in list) RemoveMarkedGet(ctx, uid);
        return list;
    }

    [CommandImplementation("clear")]
    public List<EntityUid> ClearMarked(IInvocationContext ctx)
    {
        ctx.WriteVar("marked", null);
        return [];
    }

    [CommandImplementation("clear")]
    public List<EntityUid> ClearMarked(IInvocationContext ctx, [PipedArgument] EntityUid uid) =>
        ClearMarked(ctx);

    [CommandImplementation("clear")]
    public List<EntityUid> ClearMarked(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        ClearMarked(ctx);
}
