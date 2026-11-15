using Content.Shared._Starlight.Trigger;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Temperature;
using Content.Shared.Trigger.Systems;

namespace Content.Shared.IgnitionSource;

public abstract partial class SharedIgnitionSourceSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = default!;

    #region Starlight
    private void InitializeMolotovIgnition() =>
        SubscribeLocalEvent<TriggerOnHotItemComponent, InteractUsingEvent>(OnInteractUsingHotItem);

    private void OnInteractUsingHotItem(Entity<TriggerOnHotItemComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ItemToggleComponent>(ent.Owner, out var toggle) && toggle.Activated)
            return;

        var isHot = new IsHotEvent();
        RaiseLocalEvent(args.Used, isHot);
        if (!isHot.IsHot || !_trigger.Trigger(ent.Owner, args.User, ent.Comp.KeyOut))
            return;

        args.Handled = true;
        var ev = new HotItemTriggerSucceededEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
    }
    #endregion

    /// <summary>
    /// Allows the server to maintain an active set without polling every ignition source.
    /// </summary>
    protected virtual void OnIgnitionStateChanged(Entity<IgnitionSourceComponent> ent)
    {
    }
}

[ByRefEvent]
public readonly record struct HotItemTriggerSucceededEvent;
