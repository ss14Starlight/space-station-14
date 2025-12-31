// IPC System - Battery (Server)
// SOURCE: Far-Horizons-SS14
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135
// _STARLIGHT: Namespace changes for compatibility

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

namespace Content.Server._Starlight.Silicons.IPC;

public sealed partial class IPCSystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BatteryDrainerSystem _drainer = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _state = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly PredictedBatterySystem _predictedBattery = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    protected override void SetupBattery()
    {
        base.SetupBattery();
        
        SubscribeLocalEvent<IPCBatteryComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<IPCBatteryComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<IPCBatteryComponent, MobStateChangedEvent>(OnBatteryStateChange);

        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerStart>(OnBatteryTimerStart);
        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerEnd>(OnBatteryTimerEnd);
        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerUpdate>(OnBatteryTimerUpdate);

        SubscribeLocalEvent<IPCBatteryComponent, BeingGibbedEvent>(OnBatteryGibbed);
    }

    private void OnBatteryGibbed(Entity<IPCBatteryComponent> ent, ref BeingGibbedEvent args) =>
        _containerSystem.EmptyContainer(ent.Comp.BatteryContainerSlot);
        
    private void OnBatteryTimerStart(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerStart args)
    {
        UpdateBatteryAlert(ent);
    }
    
    private void OnBatteryTimerEnd(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerEnd args)
    {
        if (!args.Interrupted)
        {
            _state.ChangeMobState(ent.Owner, MobState.Dead);
        }
        UpdateBatteryAlert(ent);
    }
    
    private void OnBatteryTimerUpdate(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerUpdate args)
    {
        if(ent.Comp.WarningText != null)
            _popup.PopupEntity(Loc.GetString(ent.Comp.WarningText), ent, PopupType.LargeCaution);
            
        // Only play alarm if cooldown has elapsed
        if(ent.Comp.WarningSound != null && _timing.CurTime >= ent.Comp.NextAlarmTime)
        {
            _audio.PlayEntity(ent.Comp.WarningSound, ent.Owner, ent.Owner);
            ent.Comp.NextAlarmTime = _timing.CurTime + ent.Comp.AlarmCooldown;
        }
    }

    private void OnBatteryStateChange(Entity<IPCBatteryComponent> ent, ref MobStateChangedEvent args)
    {
        _powerCell.SetDrawEnabled(ent.Owner, !_state.IsDead(ent));
        
        // Play critical alarm when entering critical state
        if (args.NewMobState == MobState.Critical)
        {
            _audio.PlayEntity(new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg"), ent.Owner, ent.Owner);
        }
        
        UpdateUI(ent);
    }

    protected override void UpdateBattery(float frameTime)
    {
        // When battery runs out, we begin countdown and call events as it's ticking and another event when time has ran out
        var query = EntityQueryEnumerator<IPCBatteryComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.TimerActive ||
                _timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.RefreshRate;

            comp.Timer = Math.Max(comp.Timer - (float)comp.RefreshRate.TotalSeconds, 0f);
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

    private void OnPowerCellSlotEmpty(Entity<IPCBatteryComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        StartDeathTimer(ent);
        UpdateBatteryAlert(ent);
        UpdateUI(ent);
    }
    
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

    public void StartDeathTimer(Entity<IPCBatteryComponent> ent){
        if (ent.Comp.TimerActive)
            return;
        
        ent.Comp.TimerActive = true;
        ent.Comp.WarningsIssued = 0;
        ent.Comp.Timer = ent.Comp.DieWithoutPowerAfter;
        
        // Knock down IPC immediately when power runs out (unconscious, not critical)
        if (_state.IsAlive(ent))
            _stun.TryKnockdown(ent.Owner, TimeSpan.MaxValue, autoStand: false);
            
        RaiseLocalEvent(ent, new IPCBatteryDeathTimerStart());
    }

    public void StopDeathTimer(Entity<IPCBatteryComponent> ent){
        if (!ent.Comp.TimerActive)
            return;
        
        ent.Comp.TimerActive = false;
        ent.Comp.WarningsIssued = 0;
        
        // If the timer was interrupted (power restored), remove knockdown
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

        var doAfterArgs = new DoAfterArgs(EntityManager, user, drainerComp.DrainTime, new DrainDoAfterEvent(), target: target, eventTarget: user)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void UpdateBatteryAlert(Entity<IPCBatteryComponent> ent)
    {
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
        var chargePercent = (short) MathF.Round(_predictedBattery.GetChargeLevel((batteryEnt.Owner, batteryEnt.Comp)) * 10f);

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
