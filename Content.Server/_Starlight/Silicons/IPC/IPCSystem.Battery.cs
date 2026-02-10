// IPC System - Battery (Server)
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Server.AlertLevel;
using Content.Server.Ninja.Systems;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Silicons.IPC.Components;
using Content.Shared.Alert;
using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Ninja.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server._Starlight.Silicons.IPC;

/// <summary>
/// Handles IPC battery power management and death mechanics.
/// IPCs require power to function and will enter a death timer when power runs out.
/// </summary>
public sealed partial class IPCSystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BatteryDrainerSystem _drainer = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _state = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!; // _STARLIGHT: For power loss knockdown
    [Dependency] private readonly StandingStateSystem _standing = default!; // _STARLIGHT: For power restore stand-up

    /// <summary>
    /// Sets up event subscriptions for battery-related mechanics.
    /// </summary>
    protected override void SetupBattery()
    {
        base.SetupBattery();
        
        SubscribeLocalEvent<IPCBatteryComponent, ComponentStartup>(OnServerBatteryStartup);
        SubscribeLocalEvent<IPCBatteryComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<IPCBatteryComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<IPCBatteryComponent, MobStateChangedEvent>(OnBatteryStateChange);

        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerStart>(OnBatteryTimerStart);
        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerEnd>(OnBatteryTimerEnd);
        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerUpdate>(OnBatteryTimerUpdate);

        SubscribeLocalEvent<IPCBatteryComponent, BeingGibbedEvent>(OnBatteryGibbed);
        
        // Charging DoAfter events
        SubscribeLocalEvent<IPCBatteryComponent, IPCChargeDoAfterEvent>(OnChargeDoAfter);
    }

    private void OnServerBatteryStartup(Entity<IPCBatteryComponent> ent, ref ComponentStartup args)
    {
        // Initialize battery components
        ent.Comp.PowerCellSlot = EnsureComp<PowerCellSlotComponent>(ent);
        ent.Comp.BatteryContainerSlot = _containerSystem.EnsureContainer<ContainerSlot>(ent, ent.Comp.BatteryContainerSlotID);
        
        // Get the BatteryDrainer component (already defined in prototype)
        if (TryComp<BatteryDrainerComponent>(ent, out var drainerComp))
            ent.Comp.BatteryDrainer = drainerComp;
            
        EnsureComp<PowerCellDrawComponent>(ent);
        
        // Initialize battery drainer with the IPC's power cell
        if (ent.Comp.BatteryContainerSlot.ContainedEntity != null && ent.Comp.BatteryDrainer != null)
            _drainer.SetBattery((ent, ent.Comp.BatteryDrainer), ent.Comp.BatteryContainerSlot.ContainedEntity);
    }
    
    private void OnBatteryGibbed(Entity<IPCBatteryComponent> ent, ref BeingGibbedEvent args) =>
        _containerSystem.EmptyContainer(ent.Comp.BatteryContainerSlot);
        
    private void OnBatteryTimerStart(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerStart args) =>
        UpdateBatteryAlert(ent);
    
    /// <summary>
    /// Called when the death timer ends. Kills the IPC if not interrupted.
    /// </summary>
    private void OnBatteryTimerEnd(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerEnd args)
    {
        if (!args.Interrupted)
        {
            _state.ChangeMobState(ent.Owner, MobState.Dead);
        }
        UpdateBatteryAlert(ent);
    }
    
    /// <summary>
    /// Updates during death timer - shows warnings and plays alarm sounds.
    /// </summary>
    private void OnBatteryTimerUpdate(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerUpdate args)
    {
        if(ent.Comp.WarningText != null)
            _popup.PopupEntity(Loc.GetString(ent.Comp.WarningText), ent, PopupType.LargeCaution);
            
        // Only play alarm if cooldown has elapsed (prevents spam)
        if(ent.Comp.WarningSound != null && _timing.CurTime >= ent.Comp.NextAlarmTime)
        {
            _audio.PlayEntity(ent.Comp.WarningSound, ent.Owner, ent.Owner);
            ent.Comp.NextAlarmTime = _timing.CurTime + ent.Comp.AlarmCooldown;
        }
    }

    /// <summary>
    /// Handles IPC mob state changes (alive, critical, dead).
    /// Disables power draw when dead and plays critical alarm.
    /// </summary>
    private void OnBatteryStateChange(Entity<IPCBatteryComponent> ent, ref MobStateChangedEvent args)
    {
        _powerCell.SetDrawEnabled(ent.Owner, !_state.IsDead(ent));
        
        // _STARLIGHT: Play critical alarm when entering critical state
        if (args.NewMobState == MobState.Critical)
        {
            _audio.PlayEntity(new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg"), ent.Owner, ent.Owner);
        }
        
        UpdateUI(ent);
    }

    protected override void UpdateBattery(float frameTime)
    {
        // Update all IPCs - check battery levels and death timers
        var query = EntityQueryEnumerator<IPCBatteryComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Always update battery alerts to show current charge
            if (_timing.CurTime >= comp.NextUpdate)
            {
                comp.NextUpdate = _timing.CurTime + comp.RefreshRate;
                UpdateBatteryAlert((uid, comp));
            }
            
            // Handle death timer if active
            if (comp.TimerActive)
            {
                comp.Timer = Math.Max(comp.Timer - frameTime, 0f);
                if (comp.Timer == 0f)
                {
                    StopDeathTimer((uid, comp));
                    continue;
                }

                if (comp.NumWarnings > 0)
                {
                    var step = comp.DieWithoutPowerAfter / comp.NumWarnings;
                    var should_send = Math.Ceiling(comp.NumWarnings - (comp.Timer / step));
                    if (should_send > comp.WarningsIssued)
                    {
                        RaiseLocalEvent(uid, new IPCBatteryDeathTimerUpdate());
                        comp.WarningsIssued += 1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Called when the power cell slot becomes empty.
    /// Starts the death timer immediately.
    /// </summary>
    private void OnPowerCellSlotEmpty(Entity<IPCBatteryComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        StartDeathTimer(ent);
        UpdateBatteryAlert(ent);
        UpdateUI(ent);
    }
    
    /// <summary>
    /// Called when the power cell is changed (removed or inserted).
    /// Manages death timer based on whether there's charge available.
    /// </summary>
    private void OnPowerCellChanged(Entity<IPCBatteryComponent> ent, ref PowerCellChangedEvent args)
    {
        if(!_powerCell.HasDrawCharge((ent.Owner, CompOrNull<PowerCellDrawComponent>(ent), ent.Comp.PowerCellSlot)))
            StartDeathTimer(ent);
        else
            StopDeathTimer(ent);
        
        _drainer.SetBattery((ent, ent.Comp.BatteryDrainer), ent.Comp.BatteryContainerSlot.ContainedEntity);
        UpdateBatteryAlert(ent);
        UpdateUI(ent);
    }

    /// <summary>
    /// _STARLIGHT: Starts the IPC death timer when power runs out.
    /// Immediately knocks down the IPC (makes them unconscious, not critical).
    /// After the timer expires, the IPC dies.
    /// EDIT: Added TerminatingOrDeleted check to prevent overflow when gibbed.
    /// EDIT: Added knockdown check to prevent TimeSpan overflow from repeated EMPs.
    /// </summary>
    public void StartDeathTimer(Entity<IPCBatteryComponent> ent){
        if (ent.Comp.TimerActive)
            return;
        
        // _STARLIGHT: Don't start death timer if entity is being deleted/gibbed (prevents TimeSpan overflow)
        if (TerminatingOrDeleted(ent))
            return;
        
        ent.Comp.TimerActive = true;
        ent.Comp.WarningsIssued = 0;
        ent.Comp.Timer = ent.Comp.DieWithoutPowerAfter;
        
        // _STARLIGHT: Knock down IPC immediately when power runs out (unconscious, not critical)
        // Use refresh: true to replace existing knockdown time rather than adding to it
        // This prevents TimeSpan overflow when AddKnockdownTime tries to add to existing time
        // Use a very large but safe timespan (30 days) instead of MaxValue
        // _STARLIGHT: Also check for MobStateComponent to avoid knockdown during entity deletion/gibbing
        if (_state.IsAlive(ent) && TryComp<MobStateComponent>(ent, out _))
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromDays(30), refresh: true, autoStand: false);
            
        RaiseLocalEvent(ent, new IPCBatteryDeathTimerStart());
    }

    /// <summary>
    /// _STARLIGHT: Stops the IPC death timer when power is restored.
    /// Makes the IPC stand up if they were knocked down.
    /// </summary>
    public void StopDeathTimer(Entity<IPCBatteryComponent> ent){
        if (!ent.Comp.TimerActive)
            return;
        
        ent.Comp.TimerActive = false;
        ent.Comp.WarningsIssued = 0;
        
        // _STARLIGHT: If the timer was interrupted (power restored), stand the IPC up
        var interrupted = ent.Comp.Timer != 0f;
        if (interrupted)
                _standing.Stand(ent.Owner);
        RaiseLocalEvent(ent, new IPCBatteryDeathTimerEnd(interrupted));
        ent.Comp.Timer = 0f;
    }

    protected override void StartDrain(Entity<IPCBatteryComponent> user, EntityUid target)
    {
        if (!TryComp<BatteryDrainerComponent>(user, out var drainerComp))
            return;

        // Check if battery is full before starting drain
        if (drainerComp.BatteryUid != null && _battery.IsFull(drainerComp.BatteryUid.Value))
        {
            _popup.PopupEntity(Loc.GetString("battery-drainer-full"), user, user);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, user, drainerComp.DrainTime, new DrainDoAfterEvent(), target: target, eventTarget: user)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    protected override void StartCharge(Entity<IPCBatteryComponent> user, EntityUid target)
    {
        var (uid, comp) = user;
        
        // Check if IPC battery exists and is not full
        if (!GetIPCBattery(uid, out var batteryUid, out var batteryComp))
        {
            _popup.PopupEntity(Loc.GetString("ipc-no-battery"), uid, uid);
            return;
        }

        if (_battery.IsFull((batteryUid.Value, batteryComp)))
        {
            _popup.PopupEntity(Loc.GetString("ipc-battery-full"), uid, uid);
            return;
        }

        // Check if target has power available
        if (!TryComp<BatteryComponent>(target, out var targetBattery))
        {
            _popup.PopupEntity(Loc.GetString("ipc-charge-no-battery", ("target", target)), uid, uid);
            return;
        }

        var available = _battery.GetCharge((target, targetBattery));
        if (available <= 0)
        {
            _popup.PopupEntity(Loc.GetString("ipc-charge-empty", ("target", target)), uid, uid);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.ChargeTime, new IPCChargeDoAfterEvent(), target: target, eventTarget: uid)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnChargeDoAfter(Entity<IPCBatteryComponent> ent, ref IPCChargeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not {} target)
            return;

        var (uid, comp) = ent;

        // Get IPC battery
        if (!GetIPCBattery(uid, out var batteryUid, out var batteryComp))
            return;

        // Check if battery is full
        if (_battery.IsFull((batteryUid.Value, batteryComp)))
        {
            _popup.PopupEntity(Loc.GetString("ipc-battery-full"), uid, uid);
            return;
        }

        // Get target battery
        if (!TryComp<BatteryComponent>(target, out var targetBattery))
            return;

        var available = _battery.GetCharge((target, targetBattery));
        if (available <= 0)
        {
            _popup.PopupEntity(Loc.GetString("ipc-charge-empty", ("target", target)), uid, uid);
            return;
        }

        // Calculate charge amount
        var required = batteryComp.MaxCharge - _battery.GetCharge((batteryUid.Value, batteryComp));
        var chargeAmount = Math.Min(available, required / comp.ChargeEfficiency);
        
        // Limit charging rate (similar to ninja drain)
        if (TryComp<PowerNetworkBatteryComponent>(target, out var pnb))
        {
            var maxCharged = pnb.MaxSupply * comp.ChargeTime;
            chargeAmount = Math.Min(chargeAmount, maxCharged);
        }

        // Transfer power
        if (_battery.TryUseCharge((target, targetBattery), chargeAmount))
        {
            var output = chargeAmount * comp.ChargeEfficiency;
            _battery.ChangeCharge((batteryUid.Value, batteryComp), output);
            _popup.PopupEntity(Loc.GetString("ipc-charge-success"), uid, uid);
            
            // Repeat if not full yet
            args.Repeat = !_battery.IsFull((batteryUid.Value, batteryComp));
        }
    }

    private void UpdateBatteryAlert(Entity<IPCBatteryComponent> ent)
    {
        // _STARLIGHT: Ensure power draw is enabled when alive (fixes battery not draining)
        if (_state.IsAlive(ent))
        {
            _powerCell.SetDrawEnabled(ent.Owner, true);
        }
        
        if (_state.IsAlive(ent) && ent.Comp.TimerActive && !_powerCell.HasDrawCharge((ent.Owner, CompOrNull<PowerCellDrawComponent>(ent), ent.Comp.PowerCellSlot))){
            _alerts.ClearAlertCategory(ent.Owner, ent.Comp.BatteryAlertsCategory);
            _alerts.ShowAlert(ent.Owner, ent.Comp.ChargeCritical);
            return;
        }

        if (!_powerCell.TryGetBatteryFromSlot((ent.Owner, ent.Comp.PowerCellSlot), out var battery))
        {
            _alerts.ClearAlertCategory(ent.Owner, ent.Comp.BatteryAlertsCategory);
            _alerts.ShowAlert(ent.Owner, ent.Comp.NoBatteryAlert);
            return;
        }

        var batteryEnt = battery!.Value;
        var chargePercent = (short) MathF.Round(_battery.GetChargeLevel((batteryEnt.Owner, batteryEnt.Comp)) * 10f);

        _alerts.ClearAlertCategory(ent.Owner, ent.Comp.BatteryAlertsCategory);
        _alerts.ShowAlert(ent.Owner, ent.Comp.BatteryAlert, chargePercent);
    }

    public void DrainBattery(EntityUid ent, IPCBatteryComponent? comp = null)
    {
        if (!Resolve(ent, ref comp) ||
            comp.BatteryContainerSlot.ContainedEntity == null)
            return;

        _battery.SetCharge(comp.BatteryContainerSlot.ContainedEntity.Value, 0);
    }

    /// <summary>
    /// Get the battery component in an IPC's battery slot, if it exists.
    /// Similar to ninja's GetNinjaBattery implementation.
    /// </summary>
    public bool GetIPCBattery(EntityUid user, [NotNullWhen(true)] out EntityUid? batteryUid, [NotNullWhen(true)] out BatteryComponent? batteryComp)
    {
        if (TryComp<IPCBatteryComponent>(user, out var ipcBattery)
            && _powerCell.TryGetBatteryFromSlot((user, ipcBattery.PowerCellSlot), out var battery))
        {
            batteryUid = battery.Value.Owner;
            batteryComp = battery.Value.Comp;
            return true;
        }

        batteryUid = null;
        batteryComp = null;
        return false;
    }

    /// <inheritdoc/>
    public override bool TryUseCharge(EntityUid user, float charge)
    {
        return GetIPCBattery(user, out var uid, out var battery) && _battery.TryUseCharge((uid.Value, battery), charge);
    }

    public void EjectBattery(EntityUid ent, EntityUid user, IPCBatteryComponent? comp = null)
    {
        if (!Resolve(ent, ref comp) ||
            comp.BatteryContainerSlot.ContainedEntity == null)
            return;
        
        var battery = comp.BatteryContainerSlot.ContainedEntity.Value;
        _containerSystem.EmptyContainer(comp.BatteryContainerSlot);

        _hands.PickupOrDrop(user, battery, dropNear: true);
    }
}

