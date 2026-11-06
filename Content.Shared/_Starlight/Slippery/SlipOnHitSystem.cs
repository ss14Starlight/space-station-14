using Content.Shared.Inventory;
using Content.Shared.Slippery;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Slippery;

public sealed partial class SlipOnHitSystem : EntitySystem
{
    [Dependency] private SlipperySystem _slippery = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> HardsuitTag = "Hardsuit";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlipOnHitComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, SlipOnHitComponent comp, MeleeHitEvent ev)
    {
        if (!ev.IsHit)
            return;

        var addedComp = EnsureComp<SlipperyComponent>(uid, out var slipComp);
        var savedSlipData = slipComp.SlipData;
        var savedStamDmg = slipComp.StaminaDamage;

        slipComp.SlipData = comp.SlipData;
        slipComp.StaminaDamage = comp.StaminaDamage;

        foreach (var ent in ev.HitEntities)
        {
            if (comp.BlockedByHardsuit &&
                _inventory.TryGetSlotEntity(ent, "outerClothing", out var suit) &&
                _tag.HasTag(suit.Value, HardsuitTag))
                continue;

            _slippery.TrySlip(uid, slipComp, ent, false);
        }

        slipComp.StaminaDamage = savedStamDmg;
        slipComp.SlipData = savedSlipData;
        if (addedComp)
            RemComp(uid, slipComp);
    }

}
