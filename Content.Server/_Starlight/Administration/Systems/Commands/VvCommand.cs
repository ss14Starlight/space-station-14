using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class VvCommand : ToolshedCommand
{
    [Dependency] private readonly IViewVariablesManager _vvm = default!;
    
    [CommandImplementation("write")]
    public EntityUid Write(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path, string value)
    {
        if (path.StartsWith('/')) path = path[1..];
        _vvm.WritePath($"/entity/{uid}/{path}", value);
        return uid;
    }

    [CommandImplementation("read")]
    public EntityUid Read(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path)
    {
        var val = GetViewVariable(ctx, uid, path);
        ctx.WriteLine(val ?? "null");
        return uid;
    }

    [CommandImplementation("rsave")]
    public string RSave(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path)
    {
        var val = GetViewVariable(ctx, uid, path);
        return val ?? "null";
    }

    private string? GetViewVariable(IInvocationContext ctx, EntityUid uid, string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        var val = _vvm.ReadPathSerialized($"/entity/{uid}/{path}");
        return val;
    }

    [CommandImplementation("write")]
    public IEnumerable<EntityUid> Write(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string path, string value)
        => uid.Select(x => Write(ctx, x, path, value));

    [CommandImplementation("read")]
    public IEnumerable<EntityUid> Read(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string path)
        => uid.Select(x => Read(ctx, x, path));

    [CommandImplementation("rsave")]
    public IEnumerable<string> RSave(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string path)
        => uid.Select(x=>RSave(ctx, x, path));
}