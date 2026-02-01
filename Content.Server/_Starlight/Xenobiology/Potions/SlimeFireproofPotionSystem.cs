using Content.Server.Popups;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Starlight.Xenobiology.Potions;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Interaction;

namespace Content.Server._Starlight.Xenobiology.Potions;

public sealed class SlimeFireproofPotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly TemperatureSystem _temperatureSystem = default!;
    [Dependency] private readonly FireProtectionSystem _fireProtectionSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeFireproofPotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeFireproofPotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        var successfulChange = false;
        var temperatureProtectionComponent = _entityManager.EnsureComponent<TemperatureProtectionComponent>(args.Target.Value);
        if (temperatureProtectionComponent.HeatingCoefficient > 0.0F)
        {
            _temperatureSystem.SetHeatProtection(temperatureProtectionComponent, 0.0F);
            successfulChange = true;
        }
        var fireProtectionComponent = _entityManager.EnsureComponent<FireProtectionComponent>(args.Target.Value);
        if (fireProtectionComponent.Reduction < 1.0F)
        {
            _fireProtectionSystem.SetFireProtection(fireProtectionComponent, 1.0F);
            successfulChange = true;
        }

        if (!successfulChange)
        {
            _popupSystem.PopupEntity("Fire and heat protection already at maximum. Item unaffected.", args.User, args.User);
            return;
        }
        ent.Comp.RemainingUses -= 1;
        // Yes I am avoiding localization, last time I tried it I couldn't get the plurals to work.
        // And I'm only complaining here because that seems like the best way to get help:
        // To do something wrong in the hope someone in-the-know feels the need to correct you or to do it right.
        var plural = ent.Comp.RemainingUses == 1 ? "" : "s";
        _popupSystem.PopupEntity($"Successfully applied fireproof potion! {ent.Comp.RemainingUses} use{plural} remaining.", args.User, args.User);
        if (ent.Comp.RemainingUses <= 0)
            PredictedQueueDel(args.Used);
    }
}