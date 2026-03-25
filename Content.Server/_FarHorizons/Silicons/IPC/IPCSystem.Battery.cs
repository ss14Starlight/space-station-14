using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Power;
using Content.Shared.Power.Components;

namespace Content.Server._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    protected override void SetupBattery()
    {
        base.SetupBattery();
        
        SubscribeLocalEvent<IPCBatteryComponent, PowerCellChangedEvent>((uid, comp, ev) => UpdateDeathTimer((uid, comp), ev));
        SubscribeLocalEvent<IPCBatteryComponent, ChargeChangedEvent>((uid, comp, _) => UpdateDeathTimer((uid, comp)));
        SubscribeLocalEvent<IPCBatteryComponent, BatteryStateChangedEvent>((uid, comp, _) => UpdateDeathTimer((uid, comp)));

        SubscribeLocalEvent<IPCBatteryComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<IPCBatteryComponent, MobStateChangedEvent>(OnBatteryStateChange);

        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerStart>(OnBatteryTimerStart);
        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerEnd>(OnBatteryTimerEnd);
        SubscribeLocalEvent<IPCBatteryComponent, IPCBatteryDeathTimerUpdate>(OnBatteryTimerUpdate);

        SubscribeLocalEvent<IPCBatteryComponent, BeingGibbedEvent>(OnBatteryGibbed);
    }

    private void OnBatteryGibbed(Entity<IPCBatteryComponent> ent, ref BeingGibbedEvent args) =>
        _container.EmptyContainer(ent.Comp.BatteryContainerSlot);
    private void OnBatteryTimerStart(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerStart args)
    {
        if (!TryComp<IPCReviveComponent>(ent, out var revive) || revive.DamageSoundEnt == null)
            ent.Comp.Playing = _audio.PlayPvs(ent.Comp.WarningSound, ent);
    }
    private void OnBatteryTimerEnd(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerEnd args)
    {
        if (ent.Comp.Playing == null)
            return;

        _audio.Stop(ent.Comp.Playing, ent.Comp.Playing?.Comp);
        ent.Comp.Playing = null;

        if(!args.Interrupted && TryComp<MobStateComponent>(ent, out var mobState))
        {
            _chat.TryEmoteWithChat(ent, ent.Comp.NoPowerDeathEmote);
            _state.ChangeMobState(ent, MobState.Dead, mobState);
        }
    }
    private void OnBatteryTimerUpdate(Entity<IPCBatteryComponent> ent, ref IPCBatteryDeathTimerUpdate args)
    {
        if(ent.Comp.WarningText != null)
            _popup.PopupEntity(Loc.GetString(ent.Comp.WarningText), ent, PopupType.LargeCaution);
    }

    private void OnBatteryStateChange(Entity<IPCBatteryComponent> ent, ref MobStateChangedEvent args)
    {
        _powerCell.SetDrawEnabled(ent.Owner, !_state.IsDead(ent));
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

    private void OnPowerCellSlotEmpty(Entity<IPCBatteryComponent> ent, ref PowerCellSlotEmptyEvent args) =>
        StartDeathTimer(ent);

    private void UpdateDeathTimer(Entity<IPCBatteryComponent> ent, PowerCellChangedEvent? slotChangedEv = null)
    {
        if(!_powerCell.HasDrawCharge(ent.Owner))
            StartDeathTimer(ent);
        else
            StopDeathTimer(ent);
        
        if (slotChangedEv != null && !slotChangedEv.Value.Ejected && _powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            _drainer.SetBattery(ent.Owner, battery);
    }

    public void StartDeathTimer(Entity<IPCBatteryComponent> ent){
        if (ent.Comp.TimerActive)
            return;
        
        if (_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery) && 
            (battery.Value.Comp.State != BatteryState.Empty || TryComp<BatterySelfRechargerComponent>(battery, out _)))
            return;
        
        ent.Comp.TimerActive = true;
        ent.Comp.WarningsIssued = 0;
        ent.Comp.Timer = ent.Comp.DieWithoutPowerAfter;
        RaiseLocalEvent(ent, new IPCBatteryDeathTimerStart());
    }

    public void StopDeathTimer(Entity<IPCBatteryComponent> ent){
        if (!ent.Comp.TimerActive)
            return;
        
        ent.Comp.TimerActive = false;
        ent.Comp.WarningsIssued = 0;
        RaiseLocalEvent(ent, new IPCBatteryDeathTimerEnd(ent.Comp.Timer != 0f));
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

    public void DrainBattery(Entity<IPCBatteryComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp) ||
            ent.Comp.BatteryContainerSlot.ContainedEntity == null)
            return;

        _battery.SetCharge(ent.Comp.BatteryContainerSlot.ContainedEntity.Value, 0);
    }

    public void EjectBattery(Entity<IPCBatteryComponent?> ent, EntityUid user)
    {
        
        if (!Resolve(ent, ref ent.Comp) ||
            ent.Comp.BatteryContainerSlot.ContainedEntity == null)
            return;
        
        var battery = ent.Comp.BatteryContainerSlot.ContainedEntity.Value;
        _container.EmptyContainer(ent.Comp.BatteryContainerSlot);

        _hands.PickupOrDrop(user, battery, dropNear: true);
    }
}