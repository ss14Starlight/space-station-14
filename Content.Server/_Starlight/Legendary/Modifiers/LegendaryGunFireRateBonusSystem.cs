using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._Starlight.Legendary.Modifiers;

public sealed class LegendaryGunFireRateBonusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegendaryGunFireRateBonusComponent, GunRefreshModifiersEvent>(OnRefresh);
    }
    private void OnRefresh(EntityUid uid, LegendaryGunFireRateBonusComponent component, ref GunRefreshModifiersEvent args) =>
        args.FireRate = Math.Max(0.1f, args.FireRate + component.FireRateBonus);
    
}
