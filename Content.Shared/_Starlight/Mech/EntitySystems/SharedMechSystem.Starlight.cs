using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using System.Linq;
using Content.Shared._Starlight.Mech;
using Content.Shared._Starlight.Weapons.Melee.Events;
using Content.Shared.CombatMode;
using Content.Shared.Electrocution;
using Content.Shared.Emag.Systems;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Repairable;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wires;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Mech.EntitySystems;

public abstract partial class SharedMechSystem
{
    [Dependency] private SharedAudioSystem _mechAudio = default!;
    [Dependency] private SharedPointLightSystem _mechLight = default!;

    /// <summary>
    /// Resolves a mech operator without treating other vehicles as mechs.
    /// </summary>
    public bool TryGetPilotMech(EntityUid pilot, out Entity<MechComponent> mech)
    {
        mech = default;
        if (!TryComp<VehicleOperatorComponent>(pilot, out var op) ||
            op.LifeStage > ComponentLifeStage.Running ||
            op.Vehicle is not { } vehicle ||
            !TryComp<MechComponent>(vehicle, out var comp))
            return false;

        mech = (vehicle, comp);
        return true;
    }

    private void InitializeContainers(EntityUid uid, MechComponent comp) => comp.GasTankSlot = _container.EnsureContainer<ContainerSlot>(uid, MechComponent.GasTankSlotId);

    private bool CanInstallEquipment(MechComponent comp, EntityUid equipment)
    {
        var prototype = MetaData(equipment).EntityPrototype;
        return !comp.EquipmentContainer.ContainedEntities.Any(other => MetaData(other).EntityPrototype == prototype);
    }

    /// <summary>
    /// Fires the existing module lifecycle events for every operator change, including forced removal.
    /// </summary>
    protected virtual void OperatorChanged(Entity<MechComponent> mech, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } oldPilot)
        {
            RemComp<NoRotateOnMoveComponent>(mech);
            var exited = new BeforePilotEjectEvent(mech, oldPilot);
            RaiseLocalEvent(mech, ref exited);
            foreach (var equipment in mech.Comp.EquipmentContainer.ContainedEntities.ToArray())
                RaiseLocalEvent(equipment, ref exited);
            RaiseLocalEvent(oldPilot, ref exited);
        }

        if (args.NewOperator is not { } pilot)
            return;

        if (TryComp<CombatModeComponent>(pilot, out var combat) && combat.IsInCombatMode)
            EnsureComp<NoRotateOnMoveComponent>(mech);

        var sound = mech.Comp.Integrity * 2 < mech.Comp.MaxIntegrity
            ? mech.Comp.CriticalDamageSound
            : mech.Comp.FirstStart ? mech.Comp.NominalLongSound : mech.Comp.NominalSound;
        _mechAudio.PlayPredicted(sound, mech, pilot);
        mech.Comp.FirstStart = false;

        if (_net.IsServer)
        {
            if (mech.Comp.Airtight)
                _actions.AddAction(pilot, ref mech.Comp.MechToggleInternalsActionEntity, mech.Comp.MechToggleInternalsAction, mech);
            if (_mechLight.TryGetLight(mech, out _))
                _actions.AddAction(pilot, ref mech.Comp.MechToggleLightActionEntity, mech.Comp.MechToggleLightAction, mech);
        }

        var entered = new BeforePilotInsertEvent(mech, pilot);
        RaiseLocalEvent(mech, ref entered);
        foreach (var equipment in mech.Comp.EquipmentContainer.ContainedEntities.ToArray())
            RaiseLocalEvent(equipment, ref entered);
        RaiseLocalEvent(pilot, ref entered);
        Dirty(mech);
    }

    protected static bool HasMechPower(MechComponent comp)
        => comp.MaxEnergy > 0 && (int) (comp.Energy / comp.MaxEnergy * 100) > 0;

    protected void PlayPowerSound(EntityUid mech, SoundSpecifier? sound)
        => _mechAudio.PlayPredicted(sound, mech, Vehicle.GetOperatorOrNull(mech) ?? mech);

    protected void SetInternalsAction(MechComponent comp)
        => _actions.SetToggled(comp.MechToggleInternalsActionEntity, comp.Internals);

    public void ToggleLight(EntityUid uid, MechComponent comp)
    {
        if (!_mechLight.TryGetLight(uid, out var light))
            return;
        comp.Light = !comp.Light;
        _mechLight.SetEnabled(uid, comp.Light, comp: light);
        _actions.SetToggled(comp.MechToggleLightActionEntity, comp.Light);
        PlayPowerSound(uid, comp.ToggleLightSound);
        Dirty(uid, comp);
        UpdateAppearance(uid, comp);
    }

    protected void UpdatePowerWarning(EntityUid uid, MechComponent comp)
    {
        if (comp.MaxEnergy <= 0)
            return;

        var low = (int) (comp.Energy / comp.MaxEnergy * 100) <= 33;
        if (low && comp.PlayPowerSound && Vehicle.GetOperatorOrNull(uid) is { } pilot)
        {
            _mechAudio.PlayPredicted(comp.LowPowerSound, uid, pilot);
            comp.PlayPowerSound = false;
        }
        else if (!low)
        {
            comp.PlayPowerSound = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnCanRun(Entity<MechComponent> ent, ref VehicleCanRunEvent args)
    {
        if (ent.Comp.Broken || ent.Comp.Integrity <= 0 || ent.Comp.MaintenanceMode || !HasMechPower(ent.Comp))
            args.CanRun = false;
    }

    [SubscribeLocalEvent]
    private void OnDirection(Entity<MechComponent> ent, ref ChangeDirectionAttemptEvent args)
    {
        if (ent.Comp.Broken || ent.Comp.Integrity <= 0 || ent.Comp.MaintenanceMode || !HasMechPower(ent.Comp))
            args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnMechPull(Entity<MechComponent> ent, ref PullAttemptEvent args)
    {
        if (ent.Comp.MaintenanceMode || Vehicle.HasOperator(ent.Owner))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnSpeed(Entity<MechComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.BatterySlot is { } slot && TryComp<MechSpeedModifierComponent>(slot.ContainedEntity, out var modifier))
            args.ModifySpeed(modifier.WalkModifier, modifier.SprintModifier);
    }

    [SubscribeLocalEvent]
    private void OnShoot(Entity<MechComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!ent.Comp.MaintenanceMode)
            return;
        _popup.PopupCursor(Loc.GetString("mech-turn-off-maintenance"), args.User, PopupType.MediumCaution);
        args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnRepair(Entity<MechComponent> ent, ref CanRepairEvent args)
    {
        if (ent.Comp.MaintenanceMode)
            return;
        args.Cancel();
        args.Message = Loc.GetString("mech-maintenance-mode-required");
    }

    [SubscribeLocalEvent]
    private void OnMechPanel(Entity<MechComponent> ent, ref AttemptChangePanelEvent args)
        => args.Cancelled |= args.User != ent.Owner;

    [SubscribeLocalEvent]
    private void OnEmag(Entity<MechComponent> ent, ref GotEmaggedEvent args)
    {
        if (!ent.Comp.BreakOnEmag || ent.Comp.EquipmentWhitelist == null)
            return;
        ent.Comp.EquipmentWhitelist = null;
        args.Handled = true;
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnKnockdown(Entity<VehicleOperatorComponent> ent, ref KnockDownAttemptEvent args)
    {
        if (!TryGetPilotMech(ent, out _))
            return;
        args.Cancelled = true;
        _popup.PopupCursor(Loc.GetString("mech-cant-lie-down"), ent, PopupType.SmallCaution);
    }

    [SubscribeLocalEvent]
    private void OnElectrocution(Entity<VehicleOperatorComponent> ent, ref ElectrocutionAttemptEvent args)
    {
        if (TryGetPilotMech(ent, out _))
            args.SiemensCoefficient = 0;
    }

    [SubscribeLocalEvent]
    private void OnMeleeOrigin(Entity<VehicleOperatorComponent> ent, ref GetMeleeOriginEvent args)
    {
        if (args.Handled || !TryGetPilotMech(ent, out var mech))
            return;
        args.OriginEntity = mech;
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnInternals(Entity<MechComponent> ent, ref MechToggleInternalsEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (ent.Comp.GasTankSlot.ContainedEntity == null)
        {
            if (_net.IsServer && Vehicle.GetOperatorOrNull(ent.Owner) is { } pilot)
                _popup.PopupEntity(Loc.GetString("mech-no-tank"), ent, pilot);
            return;
        }
        ent.Comp.Internals = !ent.Comp.Internals;
        _actions.SetToggled(ent.Comp.MechToggleInternalsActionEntity, ent.Comp.Internals);
        Dirty(ent);
    }
}

[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class RemoveGasTankEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
