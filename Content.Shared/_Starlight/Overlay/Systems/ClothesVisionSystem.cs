using Content.Shared.Inventory.Events;
using Content.Shared.Clothing.Components;
using Robust.Shared.Serialization.Manager;
using Content.Shared._Starlight.Shadekin;

namespace Content.Shared.Eye.Blinding.Components;

public sealed partial class ClothesVisionSystem : EntitySystem
{
    [Dependency] private readonly ISerializationManager _serialization = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothesNightVisionComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ClothesNightVisionComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(EntityUid uid, ClothesNightVisionComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing)
            || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        if (!HasComp<NightVisionComponent>(args.Equipee) || HasComp<ShadekinComponent>(args.Equipee))
        {
            var nightvision = EnsureComp<NightVisionComponent>(args.Equipee);
            nightvision.Clothes = true;
        }
    }

    private void OnUnequipped(EntityUid uid, ClothesNightVisionComponent component, GotUnequippedEvent args)
    {
        if (TryComp<NightVisionComponent>(args.Equipee, out var nightvision) && !nightvision.Clothes)
        {
            nightvision.Clothes = false;
            return;
        }

        RemComp<NightVisionComponent>(args.Equipee);
    }
}
