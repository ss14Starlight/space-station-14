using Content.Server.Power.Components;
using Content.Shared._Starlight.Light.BackupLight;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Light.BackupLight;

public sealed partial class BackupLightSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPoweredLightSystem _poweredLight = default!;
    [Dependency] private SharedPowerReceiverSystem _receiver = default!;

    private static readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + _updateInterval;

        var query = EntityQueryEnumerator<BackupLightComponent, PoweredLightComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out var backup, out var light, out var battery))
        {
            // Switching the fixture off or on doesn't raise anything we can listen to.
            UpdateRate((uid, backup, battery), light);

            if (backup.Draining
                && !backup.Flickering
                && _battery.GetChargeLevel((uid, battery)) <= backup.FlickerCharge
                && !HasComp<BlinkingPoweredLightComponent>(uid)) // Leaves lights that already blink for another reason alone.
            {
                backup.Flickering = true;
                _poweredLight.ToggleBlinkingLight(uid);
            }
        }
    }

    [SubscribeLocalEvent(after: new[] { typeof(SharedBatterySystem) })]
    private void OnMapInit(Entity<BackupLightComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<BatteryComponent>(ent, out var battery))
            return;

        UpdateRate((ent, ent.Comp, battery));

        SetCharged(ent, _battery.GetCharge((ent, battery)) > 0f);
        _poweredLight.UpdateLight(ent);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<BackupLightComponent> ent, ref PowerChangedEvent args)
    {
        if (!TryComp<BatteryComponent>(ent, out var battery))
            return;

        UpdateRate((ent, ent.Comp, battery));
        SetCharged(ent, _battery.GetCharge((ent, battery)) > 0f);

        if (args.Powered)
            StopFlicker(ent);

        // PoweredLightSystem already updated the light for this power change, but before the cell caught up.
        _poweredLight.UpdateLight(ent);
    }

    [SubscribeLocalEvent]
    private void OnRefreshChargeRate(Entity<BackupLightComponent> ent, ref RefreshChargeRateEvent args)
    {
        if (ent.Comp.Draining)
            args.NewChargeRate -= ent.Comp.DrainRate;
        else if (ent.Comp.Charging)
            args.NewChargeRate += ent.Comp.RechargeRate;
    }

    [SubscribeLocalEvent]
    private void OnBatteryStateChanged(Entity<BackupLightComponent> ent, ref BatteryStateChangedEvent args)
    {
        if (!TryComp<BatteryComponent>(ent, out var battery))
            return;

        // A full or flat cell stops charging or draining, which also takes it out of the battery update loop.
        UpdateRate((ent, ent.Comp, battery));

        if (args.NewState != BatteryState.Empty)
            return;

        StopFlicker(ent);
        SetCharged(ent, false);
        _poweredLight.UpdateLight(ent);
    }

    /// <summary>
    /// Works out whether the cell should drain or charge and passes a change on to the battery.
    /// </summary>
    private void UpdateRate(Entity<BackupLightComponent, BatteryComponent> ent, PoweredLightComponent? light = null)
    {
        var (uid, backup, battery) = ent;
        if (!Resolve(uid, ref light, false))
            return;

        var powered = _receiver.IsPowered(uid);
        var charge = _battery.GetCharge((uid, battery));

        // A switched off fixture doesn't run its cell down.
        var draining = !powered && light.On && charge > 0f && HasWorkingBulb(uid, light);
        var charging = powered && charge < battery.MaxCharge;

        if (draining == backup.Draining && charging == backup.Charging)
            return;

        if (charging != backup.Charging && TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            receiver.Load = MathF.Max(0f, receiver.Load + (charging ? backup.RechargeRate : -backup.RechargeRate));

        backup.Draining = draining;
        backup.Charging = charging;
        _battery.RefreshChargeRate((uid, battery));
    }

    private bool HasWorkingBulb(EntityUid uid, PoweredLightComponent light)
        => _poweredLight.GetBulb(uid, light) is { } bulb
            && TryComp<LightBulbComponent>(bulb, out var bulbComp)
            && bulbComp.State == LightBulbState.Normal;

    private void SetCharged(Entity<BackupLightComponent> ent, bool charged)
    {
        if (ent.Comp.Charged == charged)
            return;

        ent.Comp.Charged = charged;
        Dirty(ent);
    }

    private void StopFlicker(Entity<BackupLightComponent> ent)
    {
        if (!ent.Comp.Flickering)
            return;

        ent.Comp.Flickering = false;
        _poweredLight.ToggleBlinkingLight(ent, value: false);
    }
}
