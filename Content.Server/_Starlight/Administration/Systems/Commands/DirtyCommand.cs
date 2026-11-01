using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Debug)]
public sealed class DirtyCommand : ToolshedCommand
{
    [CommandImplementation]
    public void DirtyAll()
    {
        foreach (var uid in EntityManager.GetEntities())
        foreach (var component in EntityManager.GetNetComponents(uid))
            EntityManager.Dirty(uid, component.component);
    }

    [CommandImplementation]
    public EntityUid Dirty([PipedArgument] EntityUid uid)
    {
        foreach (var component in EntityManager.GetNetComponents(uid))
            EntityManager.Dirty(uid, component.component);
        return uid;
    }

    [CommandImplementation]
    public IEnumerable<EntityUid> Dirty([PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(Dirty);
}
