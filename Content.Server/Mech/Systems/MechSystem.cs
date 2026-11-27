using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Mech.Components;
using Content.Shared.Atmos;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Wires;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Mech.Systems;

/// <inheritdoc/>
public sealed partial class MechSystem : SharedMechSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;


    [SubscribeLocalEvent]
    private void OnRemoveBattery(EntityUid uid, MechComponent component, RemoveBatteryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        RemoveBattery(uid, component);

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnMapInit(EntityUid uid, MechComponent component, MapInitEvent args)
    {
        var xform = Transform(uid);
        // TODO: this should use containerfill?
        foreach (var equipment in component.StartingEquipment)
        {
            var ent = Spawn(equipment, xform.Coordinates);
            InsertEquipment(uid, ent, component);
        }

        // TODO: this should just be damage and battery
        component.Integrity = component.MaxIntegrity;
        component.Energy = component.MaxEnergy;
        InitializeMech(uid, component); // Starlight

        Vehicle.RefreshCanRun(uid);
        Dirty(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnToolUseAttempt(Entity<VehicleOperatorComponent> ent, ref ToolUserAttemptUseEvent args)
    {
        if (ent.Comp.Vehicle is { } vehicle && HasComp<MechComponent>(vehicle) && args.Target == vehicle) // Starlight
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnDamageChanged(EntityUid uid, MechComponent component, DamageChangedEvent args)
    {
        var integrity = component.MaxIntegrity - _damageable.GetTotalDamage((uid, args.Damageable));
        SetIntegrity(uid, integrity, component);
        Vehicle.RefreshCanRun(uid); // Starlight
    }

    [SubscribeLocalEvent]
    private void RelayGrabberUiMessage(EntityUid uid, MechComponent component, ref MechGrabberEjectMessage args) => ReceiveEquipmentUiMesssages(component, args);

    [SubscribeLocalEvent]
    private void RelaySoundboardUiMessage(EntityUid uid, MechComponent component, ref MechSoundboardPlayMessage args) => ReceiveEquipmentUiMesssages(component, args);

    private void ReceiveEquipmentUiMesssages<T>(MechComponent component, T args) where T : MechEquipmentUiMessage
    {
        var ev = new MechEquipmentUiMessageRelayEvent(args);
        var allEquipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        var argEquip = GetEntity(args.Equipment);

        foreach (var equipment in allEquipment)
        {
            if (argEquip == equipment)
                RaiseLocalEvent(equipment, ev);
        }
    }

    public override void BreakMech(EntityUid uid, MechComponent? component = null)
    {
        base.BreakMech(uid, component);

        _ui.CloseUi(uid, MechUiKey.Key);
        Vehicle.RefreshCanRun(uid);
    }

}
