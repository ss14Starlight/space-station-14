using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Wires;

namespace Content.Shared._Starlight.Light;

/// <summary>
/// Stops the bulb of a <see cref="LightFloorComponent"/> being touched while the cover is screwed shut.
/// </summary>
public sealed partial class LightFloorSystem : EntitySystem
{
    [Dependency] private SharedWiresSystem _wires = default!;
    [Dependency] private DamageOnInteractSystem _damageOnInteract = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LightFloorComponent, InteractHandEvent>(OnInteractHand,
            before: [typeof(SharedPoweredLightSystem), typeof(DamageOnInteractSystem)]);
        SubscribeLocalEvent<LightFloorComponent, InteractUsingEvent>(OnInteractUsing,
            before: [typeof(SharedPoweredLightSystem)]);
        SubscribeLocalEvent<LightFloorComponent, PanelChangedEvent>(OnPanelChanged);
    }

    /// <summary>
    /// Restores the hand burn of a lit bulb once the cover is opened, see <see cref="OnInteractHand"/>.
    /// </summary>
    private void OnPanelChanged(Entity<LightFloorComponent> ent, ref PanelChangedEvent args)
    {
        if (args.Open && TryComp<PoweredLightComponent>(ent, out var light))
            SetBurn(ent, light.CurrentLit);
    }

    /// <summary>
    /// Turns the hot-bulb hand burn on or off, if the light has one.
    /// </summary>
    private void SetBurn(EntityUid uid, bool active)
    {
        if (TryComp<DamageOnInteractComponent>(uid, out var burn))
            _damageOnInteract.SetIsDamageActiveTo((uid, burn), active);
    }

    /// <summary>
    /// Swallows empty-hand clicks on a closed floor so the bulb is not ejected and the hand is not burned.
    /// </summary>
    private void OnInteractHand(Entity<LightFloorComponent> ent, ref InteractHandEvent args)
    {
        if (_wires.IsPanelOpen(ent.Owner))
            return;

        // The burn ignores handled interactions and the light re-arms it whenever it turns on, so disarm it here.
        SetBurn(ent, false);
        args.Handled = true;
        args.InteractionParticle = false; // nothing happened, so don't show the hand
    }

    /// <summary>
    /// Swallows bulbs and light replacers used on a closed floor. Other items (tools, cables) pass through.
    /// </summary>
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
