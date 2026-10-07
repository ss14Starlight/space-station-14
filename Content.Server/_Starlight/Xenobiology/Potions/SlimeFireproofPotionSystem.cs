using Content.Server.Popups;
using Content.Server.Temperature.Systems;
using Content.Shared._Starlight.Temperature.Components;
using Content.Shared._Starlight.Xenobiology.Potions;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Interaction;

namespace Content.Server._Starlight.Xenobiology.Potions;

public sealed partial class SlimeFireproofPotionSystem : EntitySystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private TemperatureSystem _temperatureSystem = default!;
    [Dependency] private FireProtectionSystem _fireProtectionSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;

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
            _popupSystem.PopupEntity(Loc.GetString("slime-fireproof-max"), args.User, args.User);
            return;
        }
        ent.Comp.RemainingUses -= 1;
        _popupSystem.PopupEntity(Loc.GetString("slime-fireproof-applied", ("uses", ent.Comp.RemainingUses)), args.User, args.User);
        if (ent.Comp.RemainingUses <= 0)
            PredictedQueueDel(args.Used);
    }
}
