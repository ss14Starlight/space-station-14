using System.Linq;
using Content.Server.Administration;
using Content.Shared._Starlight.ViewVariables;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class VvCommand : ToolshedCommand
{
    [Dependency] private readonly IViewVariablesManager _vvm = default!;
    [Dependency] private readonly IEntityNetworkManager _net = default!;

    [CommandImplementation("open")]
    public EntityUid Vv(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _net.SendSystemNetworkMessage(new OpenViewVariablesEvent($"{EntityManager.GetNetEntity(uid).Id}"),
            ctx.Session!.Channel);
        return uid;
    }
    
    [CommandImplementation("open")]
    public void Vv(IInvocationContext ctx, [PipedArgument] string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        _net.SendSystemNetworkMessage(new OpenViewVariablesEvent($"/{path}"),
            ctx.Session!.Channel);
    }
    
    [CommandImplementation("write")]
    public EntityUid Write(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path, string value)
    {
        if (path.StartsWith('/')) path = path[1..];
        _vvm.WritePath($"/entity/{uid}/{path}", value);
        return uid;
    }

    [CommandImplementation("owrite")]
    public EntityUid ObjectWrite(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path, VarRef<object?> value)
    {
        if (path.StartsWith('/')) path = path[1..];
        var resPath = _vvm.ResolvePath($"/entity/{uid}/{path}");
        if (resPath is null)
        {
            ctx.WriteLine("Could not find path.");
            return uid;
        }
        
        var val = ctx.ReadVar(value.VarName);
        var targetType = resPath.Get()?.GetType();

        if (targetType is null)
        {
            ctx.WriteLine("Path leads to a null type.");
            return uid;
        }
        
        if (targetType != val?.GetType())
            if (!targetType.IsNullable() && val is null)
            {
                ctx.WriteLine("Type is not nullable.");
                return uid;
            }

        resPath.Set(Convert.ChangeType(val, targetType));
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

    [CommandImplementation("rsaveraw")]
    public object? RSaveRaw(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        var val = _vvm.ReadPath($"/entity/{uid}/{path}");
        return val;
    }

    private string? GetViewVariable(IInvocationContext ctx, EntityUid uid, string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        var val = _vvm.ReadPathSerialized($"/entity/{uid}/{path}");
        return val;
    }

    [CommandImplementation("open")]
    public IEnumerable<EntityUid> Vv(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => Vv(ctx, x));

    [CommandImplementation("open")]
    public void Vv(IInvocationContext ctx, [PipedArgument] IEnumerable<string> path)
    {
        foreach(var s in path) Vv(ctx, s);
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
    
    [CommandImplementation("rsaveraw")]
    public IEnumerable<object?> RSaveRaw(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string path)
        => uid.Select(x=>RSaveRaw(ctx, x, path));
}