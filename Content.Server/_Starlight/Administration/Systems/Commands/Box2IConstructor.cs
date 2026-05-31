using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[AdminCommand(AdminFlags.Fun)]
[ToolshedCommand]
public sealed class Box2IConstructorCommand : ToolshedCommand
{
    [CommandImplementation("new")]
    public EntityUid New([PipedArgument] EntityUid uid)
    {
        EnsureComp<Box2IConstructorComponent>(uid);
        return uid;
    }

    [CommandImplementation("new")]
    public IEnumerable<EntityUid> New([PipedArgument] IEnumerable<EntityUid> uid) => uid.Select(New);

    [CommandImplementation("add")]
    public EntityUid Add([PipedArgument] EntityUid uid, int x, int y, int w, int h)
    {
        var comp = Comp<Box2IConstructorComponent>(uid);
        comp.Boxes.Add(new Box2i(x, y, w, h));
        return uid;
    }

    [CommandImplementation("add")]
    public IEnumerable<EntityUid> Add([PipedArgument] IEnumerable<EntityUid> uid, int x, int y, int w, int h) =>
        uid.Select(u => Add(u, x, y, w, h));

    [CommandImplementation("clean")]
    public EntityUid Clean([PipedArgument] EntityUid uid)
    {
        RemComp<Box2IConstructorComponent>(uid);
        return uid;
    }

    [CommandImplementation("clean")]
    public IEnumerable<EntityUid> Clean([PipedArgument] IEnumerable<EntityUid> uid) => uid.Select(Clean);
}

[RegisterComponent]
public sealed partial class Box2IConstructorComponent : Component
{
    [DataField] public List<Box2i> Boxes = [];
}
