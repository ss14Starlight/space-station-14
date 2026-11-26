using Content.Server._Starlight.Mech.Components;
using Content.Shared._Starlight.Mech;
using Content.Shared.FixedPoint;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Server.Mech.Systems;

public sealed partial class MechSystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private IGameTiming _mechTiming = default!;
    [Dependency] private MovementSpeedModifierSystem _mechMovement = default!;

    private void InitializeMech(EntityUid uid, MechComponent comp)
    {
        comp.NextUpdateTime = _mechTiming.CurTime + comp.Delay;
        LinkBattery(uid, comp);
        SynchronizeBattery(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<MechComponent>();
        while (query.MoveNext(out var uid, out var mech))
        {
            if (_mechTiming.CurTime < mech.NextUpdateTime)
                continue;

            var elapsed = _mechTiming.CurTime - mech.NextUpdateTime + mech.Delay;
            mech.NextUpdateTime = _mechTiming.CurTime + mech.Delay;
            var draw = new GetPassiveChargeDrawRate(uid);
            RaiseLocalEvent(uid, draw);
            if (draw.CumulativeDrawRate > 0)
                TryChangeEnergy(uid, -draw.CumulativeDrawRate * elapsed.TotalSeconds, mech);
            SynchronizeBattery(uid, mech);
        }
    }

    private void LinkBattery(EntityUid uid, MechComponent mech)
    {
        if (mech.BatterySlot.ContainedEntity is not { } battery)
            return;
        EnsureComp<MechBatteryComponent>(battery).Mech = uid;
    }

    private void SynchronizeBattery(EntityUid uid, MechComponent mech)
    {
        var charge = 0f;
        var maximum = 0f;
        var rate = 0f;
        if (mech.BatterySlot.ContainedEntity is { } battery && TryComp<BatteryComponent>(battery, out var comp))
        {
            charge = _battery.GetCharge((battery, comp));
            maximum = comp.MaxCharge;
            rate = comp.ChargeRate;
        }

        if (mech.Energy == charge && mech.MaxEnergy == maximum)
            return;
        var changed = new ChargeChangedEvent(charge, 0, rate, maximum);
        RaiseLocalEvent(uid, ref changed);
    }

    [SubscribeLocalEvent]
    private void OnBatteryCharge(Entity<MechBatteryComponent> ent, ref ChargeChangedEvent args)
    {
        if (!TryComp<MechComponent>(ent.Comp.Mech, out var mech) || mech.BatterySlot.ContainedEntity != ent.Owner)
            return;
        var changed = args;
        RaiseLocalEvent(ent.Comp.Mech, ref changed);
    }

    [SubscribeLocalEvent]
    private void OnMechCharge(Entity<MechComponent> ent, ref ChargeChangedEvent args)
    {
        var comp = ent.Comp;
        comp.Energy = args.CurrentCharge;
        comp.MaxEnergy = args.MaxCharge;
        if (!HasMechPower(comp))
        {
            if (comp.Light)
                ToggleLight(ent, comp);
            if (!comp.PlayPowerUpSound)
                PlayPowerSound(ent, comp.PowerDownSound);
            comp.PlayPowerUpSound = true;
        }
        else
        {
            if (comp.PlayPowerUpSound)
            {
                PlayPowerSound(ent, comp.PowerupSound);
                comp.PlayPowerUpSound = false;
                comp.PlayPowerSound = true;
            }
            UpdatePowerWarning(ent, comp);
        }

        Dirty(ent);
        Vehicle.RefreshCanRun(ent.Owner);
        UpdateUserInterface(ent, comp);
    }

    [SubscribeLocalEvent]
    private void OnItemInserted(Entity<MechComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container == ent.Comp.BatterySlot)
        {
            LinkBattery(ent, ent.Comp);
            SynchronizeBattery(ent, ent.Comp);
            _mechMovement.RefreshMovementSpeedModifiers(ent);
        }
        UpdateUserInterface(ent, ent.Comp);
    }

    [SubscribeLocalEvent]
    private void OnItemRemoved(Entity<MechComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container == ent.Comp.BatterySlot)
        {
            if (TryComp<MechBatteryComponent>(args.Entity, out var battery) && battery.Mech == ent.Owner)
                RemComp<MechBatteryComponent>(args.Entity);
            SynchronizeBattery(ent, ent.Comp);
            _mechMovement.RefreshMovementSpeedModifiers(ent);
        }
        else if (args.Container == ent.Comp.GasTankSlot)
        {
            DisableInternals(ent, ent.Comp);
        }
        Dirty(ent);
        UpdateUserInterface(ent, ent.Comp);
    }

    public override bool TryChangeEnergy(EntityUid uid, FixedPoint2 delta, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.BatterySlot.ContainedEntity is not { } battery ||
            !TryComp<BatteryComponent>(battery, out var batteryComp))
            return false;

        if (delta < 0)
        {
            if (!_battery.TryUseCharge((battery, batteryComp), -delta.Float()))
                return false;
        }
        else
        {
            _battery.ChangeCharge((battery, batteryComp), delta.Float());
        }
        SynchronizeBattery(uid, component);
        return true;
    }

    public void InsertBattery(EntityUid uid, EntityUid toInsert, MechComponent? component = null, BatteryComponent? battery = null)
    {
        if (!Resolve(uid, ref component, false) || !Resolve(toInsert, ref battery, false) ||
            !_mechWhitelist.IsWhitelistPassOrNull(component.BatteryWhitelist, toInsert) ||
            !_container.Insert(toInsert, component.BatterySlot))
            return;
        LinkBattery(uid, component);
        SynchronizeBattery(uid, component);
        Vehicle.RefreshCanRun(uid);
    }

    public void RemoveBattery(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        _container.EmptyContainer(component.BatterySlot);
        SynchronizeBattery(uid, component);
        Vehicle.RefreshCanRun(uid);
    }
}
