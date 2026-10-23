using Content.Server.Construction;
using Content.Shared._Starlight.Trigger;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Temperature;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Timing;

namespace Content.Server.IgnitionSource;

public sealed partial class IgnitionSourceSystem
{
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private ConstructionSystem _construction = default!;

    private void InitializeMolotovIgnition()
    {
        SubscribeLocalEvent<TriggerOnHotItemComponent, InteractUsingEvent>(OnInteractUsingHotItem);
    }

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
        var uid = ent.Owner;
        Timer.Spawn(TimeSpan.FromSeconds(45), () =>
        {
            if (!Exists(uid) || !TryComp<ItemToggleComponent>(uid, out var toggle) || !toggle.Activated)
                return;

            _construction.ChangeNode(uid, null, "burntout");
        });
    }
}
