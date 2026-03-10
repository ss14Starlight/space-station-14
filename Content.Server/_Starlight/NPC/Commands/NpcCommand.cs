using System.Linq;
using Content.Server.Administration;
using Content.Server.NPC.HTN;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.NPC.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class NpcCommand : ToolshedCommand
{
    private HTNSystem? _htn;
    
    [CommandImplementation("sethtn")]
    public EntityUid SetHTN([PipedArgument] EntityUid uid, ProtoId<HTNCompoundPrototype> htnCompound)
    {
        var comp = EnsureComp<HTNComponent>(uid);
        comp.RootTask = new HTNCompoundTask()
        {
            Task = htnCompound
        };
        return uid;
    }

    [CommandImplementation("setenabled")]
    public EntityUid SetEnabled([PipedArgument] EntityUid uid, bool enabled)
    {
        _htn ??= EntitySystemManager.GetEntitySystem<HTNSystem>();
        var comp = EnsureComp<HTNComponent>(uid);
        _htn.SetHTNEnabled((uid, comp), enabled);
        return uid;
    }

    [CommandImplementation("sethtn")]
    public IEnumerable<EntityUid> SetHTN([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<HTNCompoundPrototype> htnCompound)
        => uid.Select(x => SetHTN(x, htnCompound));

    [CommandImplementation("setenabled")]
    public IEnumerable<EntityUid> SetEnabled([PipedArgument] IEnumerable<EntityUid> uid, bool enabled)
        => uid.Select(x => SetEnabled(x, enabled));
}