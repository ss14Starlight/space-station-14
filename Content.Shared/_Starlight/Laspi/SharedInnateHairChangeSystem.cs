using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.MagicMirror;

namespace Content.Shared._Starlight.Laspi;

public sealed partial class SharedInnateHairChangeSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<InnateHairChangeComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.ActionProto, ent);
        EnsureComp<MagicMirrorComponent>(ent);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<InnateHairChangeComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp(ent, out ActionsComponent? comp))
            return;

        var actions = new Entity<ActionsComponent?>(ent, comp);
        _actions.RemoveAction(actions, ent.Comp.ActionEntity);
    }
}
