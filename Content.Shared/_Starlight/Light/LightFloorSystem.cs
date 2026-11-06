using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Wires;

namespace Content.Shared._Starlight.Light;

public sealed partial class LightFloorSystem : EntitySystem
{
    [Dependency] private SharedWiresSystem _wires = default!;
    [Dependency] private DamageOnInteractSystem _damageOnInteract = default!;

    [SubscribeLocalEvent]
    private void OnGetLightLevel(Entity<LightFloorComponent> ent, ref GetLightFixtureLevelEvent args)
    {
        args.LightEnergy *= ent.Comp.EnergyMultiplier;
        args.LightRadius = Math.Min(args.LightRadius, ent.Comp.MaxRadius);
    }

    [SubscribeLocalEvent]
    private void OnPanelChanged(Entity<LightFloorComponent> ent, ref PanelChangedEvent args)
    {
        if (args.Open && TryComp<PoweredLightComponent>(ent, out var light))
            SetBurn(ent, light.CurrentLit);
    }

    private void SetBurn(EntityUid uid, bool active)
    {
        if (TryComp<DamageOnInteractComponent>(uid, out var burn))
            _damageOnInteract.SetIsDamageActiveTo((uid, burn), active);
    }

    [SubscribeLocalEvent(before: [typeof(SharedPoweredLightSystem), typeof(DamageOnInteractSystem)])]
    private void OnInteractHand(Entity<LightFloorComponent> ent, ref InteractHandEvent args)
    {
        if (_wires.IsPanelOpen(ent.Owner))
            return;

        // The burn ignores handled interactions and the light re-arms it whenever it turns on, so disarm it here.
        SetBurn(ent, false);
        args.Handled = true;
        args.InteractionParticle = false; // nothing happened, so don't show the hand
    }

    [SubscribeLocalEvent(before: [typeof(SharedPoweredLightSystem)])]
    private void OnEjectDoAfter(EntityUid uid, LightFloorComponent comp, PoweredLightDoAfterEvent args)
    {
        if (!args.Cancelled && !_wires.IsPanelOpen(uid))
            args.Handled = true;
    }

    [SubscribeLocalEvent(before: [typeof(SharedPoweredLightSystem)])]
    private void OnInteractUsing(Entity<LightFloorComponent> ent, ref InteractUsingEvent args)
    {
        if (_wires.IsPanelOpen(ent.Owner))
            return;

        if (!HasComp<LightBulbComponent>(args.Used) && !HasComp<LightReplacerComponent>(args.Used))
            return;

        args.Handled = true;
        args.InteractionParticle = false;
    }
}
