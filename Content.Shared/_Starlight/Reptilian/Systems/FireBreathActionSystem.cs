using Content.Shared._Starlight.Reptilian.Components;
using Content.Shared.Actions;

namespace Content.Shared._Starlight.Reptilian.Systems;

/// <summary>
/// Grants and removes the fire breath action. The breath itself is handled on the server.
/// </summary>
public sealed partial class FireBreathActionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<FireBreathComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FireBreathComponent> ent, ref ComponentShutdown args)
        => _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
}
