using System.Linq;
using Content.Shared.Atmos.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Toggleable;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Mech.Systems;

public sealed partial class MechSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedToolSystem _toolSystem = default!;
    [Dependency] private EntityWhitelistSystem _mechWhitelist = default!;
    [Dependency] private NpcFactionSystem _mechFactions = default!;
    [Dependency] private SharedPopupSystem _mechPopup = default!;
    [Dependency] private SharedWiresSystem _mechWires = default!;

    private static readonly ProtoId<ToolQualityPrototype> _pryingQuality = "Prying";

    protected override void OperatorChanged(Entity<MechComponent> mech, ref VehicleOperatorSetEvent args)
    {
        base.OperatorChanged(mech, ref args);
        if (args.OldOperator != null)
            RemComp<NpcFactionMemberComponent>(mech);
        if (args.NewOperator is { } pilot)
        {
            _ui.CloseUis(mech.Owner);
            _mechFactions.Up(pilot, mech);
        }
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<MechComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || (TryComp<WiresPanelComponent>(ent, out var panel) && !panel.Open))
            return;

        if (ent.Comp.BatterySlot.ContainedEntity == null && TryComp<BatteryComponent>(args.Used, out var battery) &&
            _mechWhitelist.IsWhitelistPassOrNull(ent.Comp.BatteryWhitelist, args.Used))
        {
            InsertBattery(ent, args.Used, ent.Comp, battery);
            args.Handled = ent.Comp.BatterySlot.ContainedEntity == args.Used;
            return;
        }
        if (ent.Comp.GasTankSlot.ContainedEntity == null && TryComp<GasTankComponent>(args.Used, out var gasTank))
        {
            InsertGasTank(ent, args.Used, ent.Comp, gasTank);
            args.Handled = ent.Comp.GasTankSlot.ContainedEntity == args.Used;
            return;
        }
        if (!_toolSystem.HasQuality(args.Used, _pryingQuality))
            return;

        SimpleDoAfterEvent removal;
        if (ent.Comp.BatterySlot.ContainedEntity != null)
            removal = new RemoveBatteryEvent();
        else if (ent.Comp.GasTankSlot.ContainedEntity != null)
            removal = new RemoveGasTankEvent();
        else
            return;

        var after = new DoAfterArgs(EntityManager, args.User, ent.Comp.BatteryRemovalDelay, removal,
            ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
        };
        args.Handled = _doAfter.TryStartDoAfter(after);
    }

    [SubscribeLocalEvent]
    private void OnToggleLight(Entity<MechComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled || ent.Comp.BatterySlot.ContainedEntity is not { } battery ||
            !TryComp<BatteryComponent>(battery, out var comp) || _battery.GetCharge((battery, comp)) <= 0)
            return;
        args.Handled = true;
        ToggleLight(ent, ent.Comp);
    }

    [SubscribeLocalEvent]
    private void OnMaintenance(Entity<MechComponent> ent, ref MechMaintenanceUiMessage args)
    {
        if (Vehicle.GetOperatorOrNull(ent.Owner) is { } pilot && args.Actor != pilot && args.Actor != ent.Owner)
            return;
        SetMaintenanceMode(ent, args.Toggle, ent.Comp);
    }

    public void SetMaintenanceMode(EntityUid uid, bool enabled, MechComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;
        if (TryComp<WiresPanelComponent>(uid, out var panel))
            _mechWires.TogglePanel(uid, panel, enabled, uid);
        if (comp.MaintenanceMode != enabled)
        {
            var message = enabled ? "mech-maintenance-enabled" : "mech-maintenance-disabled";
            _mechPopup.PopupPredicted(Loc.GetString(message), uid, Vehicle.GetOperatorOrNull(uid));
            PlayPowerSound(uid, enabled ? comp.MaintenanceOnSound : comp.MaintenanceOffSound);
        }
        comp.MaintenanceMode = enabled;
        Dirty(uid, comp);
        Vehicle.RefreshCanRun(uid);
        UpdateUserInterface(uid, comp);
    }

    [SubscribeLocalEvent]
    private void OnRemoveEquipment(Entity<MechComponent> ent, ref MechEquipmentRemoveMessage args)
    {
        if (!ent.Comp.MaintenanceMode)
        {
            _mechPopup.PopupEntity(Loc.GetString("mech-requires-maintenance-mode"), ent, PopupType.MediumCaution);
            return;
        }
        var equipment = GetEntity(args.Equipment);
        if (Exists(equipment) && !Deleted(equipment) && ent.Comp.EquipmentContainer.ContainedEntities.Contains(equipment))
            RemoveEquipment(ent, equipment, ent.Comp);
    }

    public override void UpdateUserInterface(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.EquipmentContainer == null)
            return;
        base.UpdateUserInterface(uid, component);
        var ready = new MechEquipmentUiStateReadyEvent();
        foreach (var equipment in component.EquipmentContainer.ContainedEntities)
            RaiseLocalEvent(equipment, ready);
        _ui.SetUiState(uid, MechUiKey.Key, new MechBoundUiState
        {
            EquipmentStates = ready.States,
            Equipment = component.EquipmentContainer.ContainedEntities.Select(equipment => GetNetEntity(equipment)).ToList(),
        });
    }
}
