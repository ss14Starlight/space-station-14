using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.MagicMirror;

namespace Content.Shared._Starlight.Laspi;

/// <summary>
/// System that handles the InnateHairChangeComponent, which allows an entity to change their hair/facial hair using the magic mirror UI.
/// This is primarily just for the Laspi and neo-Laspi species, but there's nothing stopping you from adding it to other species if you wanna be a hair wizard or something. :3
/// </summary>
public sealed class SharedInnateHairChangeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    /// <summary>
    /// Subscribe to the map start and component shutdown events so we can add/remove the InnateHairChange action for adding the action later.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InnateHairChangeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InnateHairChangeComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>
    /// When the map starts, add the InnateHairChange action to your blorbo and ensure it has a MagicMirrorComponent.
    /// </summary>
    private void OnMapInit(Entity<InnateHairChangeComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.ActionProto, ent);
        EnsureComp<MagicMirrorComponent>(ent);
    }


    /// <summary>
    /// Clean up the actions when the component is removed.
    /// </summary>
    private void OnShutdown(Entity<InnateHairChangeComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp(ent, out ActionsComponent? comp))
            return;

        var actions = new Entity<ActionsComponent?>(ent, comp);
        _actions.RemoveAction(actions, ent.Comp.ActionEntity);
    }
}
