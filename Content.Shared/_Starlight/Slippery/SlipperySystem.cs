using Content.Shared.Slippery;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Starlight.Slippery;

public sealed partial class SlipOnHitSystem : EntitySystem
{
    [Dependency] private readonly SlipperySystem _slippery = default!;

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

        slipComp.SlipData = comp.SlipData;
        foreach (var ent in ev.HitEntities)
        {
            _slippery.TrySlip(uid, slipComp, ent, false);
        }
        slipComp.SlipData = savedSlipData;
        if (addedComp)
            RemComp(uid, slipComp);
    }

}
