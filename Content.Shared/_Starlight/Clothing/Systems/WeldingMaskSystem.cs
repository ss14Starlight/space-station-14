using Content.Shared._Starlight.Clothing.Components;
using Content.Shared._Starlight.Eye;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Eye;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Shared._Starlight.Clothing.Systems;

public sealed partial class WeldingMaskSystem : EntitySystem
{
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedDarkenedVisionSystem _darkenedVision = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeldingMaskComponent, ItemToggledEvent>(OnToggle);
    }

    private void OnToggle(Entity<WeldingMaskComponent> ent, ref ItemToggledEvent args)
    {
        var prefix = args.Activated ? null : "up";
        _clothing.SetEquippedPrefix(ent, prefix);
        _item.SetHeldPrefix(ent, prefix);

        if (args.User is not { } user) return;

        _darkenedVision.UpdateVisionDarkening(user);

        // update identity
        if (args.Activated && TryComp<MaskComponent>(ent.Owner, out var maskComp))
        {
            var ev = new WearerMaskToggledEvent((ent.Owner, maskComp));
            RaiseLocalEvent(user, ref ev);
        }
    }
}
