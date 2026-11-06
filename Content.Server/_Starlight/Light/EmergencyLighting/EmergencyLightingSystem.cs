using Content.Shared._Starlight.Light.EmergencyLighting;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Light.EmergencyLighting;

/// <summary>
/// Runs down and charges the emergency cells of light fixtures, see <see cref="EmergencyLightingComponent"/>.
/// The light itself is handled by <see cref="SharedPoweredLightSystem"/>.
/// </summary>
public sealed partial class EmergencyLightingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
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

        var query = EntityQueryEnumerator<EmergencyLightingComponent, PoweredLightComponent>();
        while (query.MoveNext(out var uid, out var comp, out var light))
        {
            if (!comp.Discharging || !comp.Charged)
                continue;

            if (!light.On)
            {
                comp.ChargeUpdated = now;
                continue;
            }

            SyncCharge(comp, now);

            if (comp.Charge <= 0f)
            {
                StopFlicker((uid, comp));
                comp.Charged = false;
                Dirty(uid, comp);
                _poweredLight.UpdateLight(uid);
            }
            else if (comp.Charge <= comp.FlickerCharge && !comp.Flickering && !HasComp<BlinkingPoweredLightComponent>(uid))
            {
                comp.Flickering = true;
                _poweredLight.ToggleBlinkingLight(uid);
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<EmergencyLightingComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ChargeUpdated = _timing.CurTime;
        ent.Comp.Discharging = !_receiver.IsPowered(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<EmergencyLightingComponent> ent, ref PowerChangedEvent args)
    {
        var comp = ent.Comp;
        SyncCharge(comp, _timing.CurTime);
        comp.Discharging = !args.Powered;

        if (!comp.Discharging)
            StopFlicker(ent);

        var charged = comp.Charge > 0f;
        if (comp.Charged != charged)
        {
            comp.Charged = charged;
            Dirty(ent);
        }

        if (TryComp<PoweredLightComponent>(ent, out var light))
            _poweredLight.UpdateLight(ent, light);
    }

    private static void SyncCharge(EmergencyLightingComponent comp, TimeSpan now)
    {
        var elapsed = (float) (now - comp.ChargeUpdated).TotalSeconds;
        comp.ChargeUpdated = now;
        if (elapsed <= 0f)
            return;

        comp.Charge = comp.Discharging
            ? MathF.Max(0f, comp.Charge - (elapsed / (float) comp.Duration.TotalSeconds))
            : MathF.Min(1f, comp.Charge + (elapsed / (float) comp.RechargeTime.TotalSeconds));
    }

    private void StopFlicker(Entity<EmergencyLightingComponent> ent)
    {
        if (!ent.Comp.Flickering)
            return;

        ent.Comp.Flickering = false;
        _poweredLight.ToggleBlinkingLight(ent, value: false);
    }
}
