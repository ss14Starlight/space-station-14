using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.MagicMirror;

namespace Content.Shared._Starlight.Laspi;

public sealed class SharedInnateHairChangeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InnateHairChangeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InnateHairChangeComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<InnateHairChangeComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.ActionProto, ent);
        // Make sure the entity can actually BE a mirror
        EnsureComp<MagicMirrorComponent>(ent);
    }

    private void OnShutdown(Entity<InnateHairChangeComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp(ent, out ActionsComponent? comp))
            return;

        var actions = new Entity<ActionsComponent?>(ent, comp);
        _actions.RemoveAction(actions, ent.Comp.ActionEntity);
    }
}
