using System.Linq;
using Content.Server.Administration;
using Content.Shared._Starlight.Components;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand(Name="ccomp"), AdminCommand(AdminFlags.Fun)]
public sealed class ClientCompCommand : ToolshedCommand
{
    private static readonly string AddedPrefix = "Attempted to add component with the name";
    private static readonly string AddedSuffix = "to the entity all clients.";
    private static readonly string WritePrefix = "Attempted to vvwrite";
    private static readonly string WriteInfix = "into";
    private static readonly string WriteSuffix = "on all clients.";
    private static readonly string RemovePrefix = "Attempted to remove";
    private static readonly string RemoveSuffix = "from the entity on all clients.";

    [CommandImplementation("ensure")]
    public EntityUid Ensure(IInvocationContext ctx, [PipedArgument] EntityUid uid, string compName)
    {
        var name = compName.Trim().ToLower();
        var comp = EnsureComp<ClientCompControlComponent>(uid);
        comp.EnsuredComponents.Add(name);
        comp.RemovedComponents.Remove(name);
        EntityManager.Dirty(uid, comp);
        ctx.WriteLine($"{AddedPrefix} {name} {AddedSuffix}");
        return uid;
    }

    [CommandImplementation("write")]
    public EntityUid Write(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path, string value)
    {
        var comp = EnsureComp<ClientCompControlComponent>(uid);
        if (!path.StartsWith('/')) path = $"/{path}";
        comp.ViewVariablesWrites[path] = value;
        EntityManager.Dirty(uid, comp);
        ctx.WriteLine($"{WritePrefix} {value} {WriteInfix} {path} {WriteSuffix}");
        return uid;
    }

    [CommandImplementation("rm")]
    public EntityUid Rm(IInvocationContext ctx, [PipedArgument] EntityUid uid, string compName)
    {
        var name = compName.Trim().ToLower();
        var comp = EnsureComp<ClientCompControlComponent>(uid);
        comp.EnsuredComponents.Remove(name);
        comp.RemovedComponents.Add(name);
        foreach (var vvwrite in from vvwrite in comp.ViewVariablesWrites.ToList()
                 let key = vvwrite.Key
                 where key.StartsWith('/')
                 let slashIndex = key.IndexOf('/', 1)
                 let componentName = slashIndex == -1
                     ? key[1..]
                     : key[1..slashIndex]
                 where componentName.Equals(name, StringComparison.OrdinalIgnoreCase)
                 select vvwrite)
            comp.ViewVariablesWrites.Remove(vvwrite.Key);
        EntityManager.Dirty(uid, comp);
        ctx.WriteLine($"{RemovePrefix} {name} {RemoveSuffix}");
        return uid;
    }

    [CommandImplementation("ensure")]
    public IEnumerable<EntityUid> Ensure(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string compName)
        => uid.Select(x => Ensure(ctx, x, compName));

    [CommandImplementation("write")]
    public IEnumerable<EntityUid> Write(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string path, string value)
        => uid.Select(x => Write(ctx, x, path, value));

    [CommandImplementation("rm")]
    public IEnumerable<EntityUid> Rm(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string compName)
        => uid.Select(x => Rm(ctx, x, compName));
}
