using Content.Server._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Shadekin;

public sealed class TheDarkImmuneSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<TheDarkImmuneComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<TheDarkImmuneComponent, GotUnequippedEvent>((uid, _, args) => RemComp<TheDarkImmuneComponent>(args.Equipee));
    }

    private void OnEquipped(EntityUid uid, TheDarkImmuneComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing)
            || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        EnsureComp<TheDarkImmuneComponent>(args.Equipee);
    }
}
