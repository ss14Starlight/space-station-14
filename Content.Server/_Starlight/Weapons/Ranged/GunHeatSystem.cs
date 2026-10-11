using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Starlight.FiringPins;
using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared._Starlight.Weapons.Ranged.Systems;
using Content.Shared.Atmos;
using Content.Shared.Popups;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;

namespace Content.Server._Starlight.Weapons.Ranged;

public sealed partial class GunHeatSystem : SharedGunHeatSystem
{
    [Dependency] private TemperatureSystem _temperature = default!;

    private const float SyncStep = 2f;

    private static readonly TimeSpan _passiveCoolingInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextPassiveCooling;

    private bool _firing;

    private static readonly TimeSpan _syncHold = TimeSpan.FromSeconds(2);

    private readonly Dictionary<EntityUid, TimeSpan> _lastShot = new();

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<GunHeatComponent> ent, ref MapInitEvent _)
    {
        var temperature = EnsureComp<TemperatureComponent>(ent);
        temperature.AtmosTemperatureTransferEfficiency = ent.Comp.CoolingEfficiency;
        EnsureComp<AtmosExposedComponent>(ent);

        var heatCapacity = _temperature.GetHeatCapacity(ent, temperature);
        ent.Comp.TemperaturePerShot = heatCapacity > 0f ? ent.Comp.HeatPerShot / heatCapacity : 0f;

        Sync(ent, temperature.CurrentTemperature, force: true);
    }

    protected override void AddShotHeat(Entity<GunHeatComponent> ent, int shots)
    {
        if (!TryComp<TemperatureComponent>(ent, out var temperature))
            return;

        _lastShot[ent] = Timing.CurTime;

        _firing = true;
        try
        {
            _temperature.ChangeHeat(ent, ent.Comp.HeatPerShot * shots, ignoreHeatResistance: true, temperature);
        }
        finally
        {
            _firing = false;
        }
    }

    [SubscribeLocalEvent]
    private void OnModifyTemperature(EntityUid uid, GunHeatComponent component, ModifyChangedTemperatureEvent args)
    {
        if (_firing || args.TemperatureDelta <= 0f || !TryComp<TemperatureComponent>(uid, out var temperature))
            return;

        var limit = component.JamTemperature - component.EnvironmentHeatMargin;
        var room = (limit - temperature.CurrentTemperature) * _temperature.GetHeatCapacity(uid, temperature);
        args.TemperatureDelta = Math.Clamp(args.TemperatureDelta, 0f, Math.Max(0f, room));
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<GunHeatComponent> ent, ref ComponentShutdown args)
        => _lastShot.Remove(ent);

    [SubscribeLocalEvent]
    private void OnTemperatureChange(Entity<GunHeatComponent> ent, ref OnTemperatureChangeEvent args)
    {
        Sync(ent, args.CurrentTemperature);

        if (_firing && args.CurrentTemperature >= ent.Comp.MeltTemperature)
            MeltFiringPin(ent);
    }

    private void Sync(Entity<GunHeatComponent> ent, float temperature, bool force = false)
    {
        if (!force && MathF.Abs(temperature - ent.Comp.Temperature) < SyncStep)
            return;

        if (!force && _lastShot.TryGetValue(ent, out var lastShot) && Timing.CurTime < lastShot + _syncHold)
            return;

        ent.Comp.Temperature = temperature;
        Dirty(ent);
    }

    private void MeltFiringPin(Entity<GunHeatComponent> ent)
    {
        if (!TryComp<FiringPinHolderComponent>(ent, out var holder) || holder.PinContainer.ContainedEntities.Count == 0)
            return;

        foreach (var pin in holder.PinContainer.ContainedEntities.ToArray())
        {
            var pinComp = CompOrNull<FiringPinComponent>(pin);

            if ((pinComp?.MeltedPrototype ?? ent.Comp.MeltedPin) is { } melted)
                SpawnNextToOrDrop(melted, ent);

            Audio.PlayPvs(pinComp?.MeltedSound ?? ent.Comp.MeltSound, ent);
            Popup.PopupEntity(Loc.GetString(pinComp?.MeltedPopup.Id ?? "gun-heat-pin-melted", ("gun", ent.Owner)), ent, PopupType.MediumCaution);

            QueueDel(pin);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Timing.CurTime < _nextPassiveCooling)
            return;

        _nextPassiveCooling = Timing.CurTime + _passiveCoolingInterval;

        // The air cools guns through the temperature system; this keeps them from staying hot forever in space.
        var query = EntityQueryEnumerator<GunHeatComponent, TemperatureComponent>();
        while (query.MoveNext(out var uid, out var heat, out var temperature))
        {
            var excess = temperature.CurrentTemperature - Atmospherics.T20C;
            if (excess <= 0.5f)
                continue;

            var seconds = (float) _passiveCoolingInterval.TotalSeconds;
            var heatLoss = MathF.Min(excess * heat.PassiveCooling * seconds, excess * _temperature.GetHeatCapacity(uid, temperature));
            _temperature.ChangeHeat(uid, -heatLoss, ignoreHeatResistance: true, temperature);
        }
    }
}
