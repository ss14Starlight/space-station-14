using Content.Server.Construction;
using Content.Shared._Starlight.Trigger;
using Content.Shared.IgnitionSource;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Timing;

namespace Content.Server.IgnitionSource;

public sealed partial class IgnitionSourceSystem
{
    [Dependency] private ConstructionSystem _construction = default!;

    private void InitializeMolotovBurnout() =>
        SubscribeLocalEvent<TriggerOnHotItemComponent, HotItemTriggerSucceededEvent>(OnHotItemTriggerSucceeded);

    private void OnHotItemTriggerSucceeded(Entity<TriggerOnHotItemComponent> ent, ref HotItemTriggerSucceededEvent args)
    {
        var uid = ent.Owner;
        Timer.Spawn(TimeSpan.FromSeconds(45), () =>
        {
            if (!Exists(uid) || !TryComp<ItemToggleComponent>(uid, out var toggle) || !toggle.Activated)
                return;

            _construction.ChangeNode(uid, null, "burntout");
        });
    }
}
