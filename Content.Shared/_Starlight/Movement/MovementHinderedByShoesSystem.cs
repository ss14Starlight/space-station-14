using Content.Shared._Starlight.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared._Starlight.Movement.Components;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;

namespace Content.Shared._Starlight.Movement;

public sealed partial class MovementHinderedByShoesSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SLBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SLBodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<SLBodyComponent> body, ref RefreshMovementSpeedModifiersEvent args)
    {
        // shoes check
        if (!_inventory.TryGetSlotEntity(body, "shoes", out var entityUid))
            return;


        float hinderModifier = 0f;

        foreach (var leg in _body.GetBodyPartsWithComp<MovementBodyPartHinderedByShoesComponent>(body))
            hinderModifier += leg.Comp2.HinderModifier;

        if (TryComp<OverrideShoesHinderComponent>(entityUid, out var _override))
        {
            hinderModifier *= _override.HinderModifier;
        }
        if (hinderModifier > 0f)
        {
            args.ModifySpeed(1f, 1f - hinderModifier);
        }
    }
}
