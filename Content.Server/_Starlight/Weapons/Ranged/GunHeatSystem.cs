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
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Weapons.Ranged;

public sealed partial class GunHeatSystem : SharedGunHeatSystem
{
    [Dependency] private TemperatureSystem _temperature = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const float SyncStep = 2f;

    private static readonly TimeSpan _passiveCoolingInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextPassiveCooling;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<GunHeatComponent> ent, ref MapInitEvent _)
    {
        var temperature = EnsureComp<TemperatureComponent>(ent);
        temperature.AtmosTemperatureTransferEfficiency = ent.Comp.CoolingEfficiency;
        EnsureComp<AtmosExposedComponent>(ent);
        Sync(ent, temperature.CurrentTemperature, force: true);
    }

    [SubscribeLocalEvent]
    private void OnGunShot(Entity<GunHeatComponent> ent, ref GunShotEvent args)
    {
        if (args.Ammo.Count == 0 || !TryComp<TemperatureComponent>(ent, out var temperature))
            return;

        var kelvin = ent.Comp.HeatPerShot * args.Ammo.Count;
        _temperature.ChangeHeat(ent, kelvin * _temperature.GetHeatCapacity(ent, temperature), ignoreHeatResistance: true, temperature);

        if (ent.Comp.Jammed || !_random.Prob(GetJamChance(ent.Comp, temperature.CurrentTemperature)))
            return;

        ent.Comp.Jammed = true;
        Dirty(ent);
        Audio.PlayPvs(ent.Comp.JamSound, ent);
        Popup.PopupEntity(Loc.GetString("gun-heat-jammed"), ent, args.User, PopupType.SmallCaution);
    }

    [SubscribeLocalEvent]
    private void OnTemperatureChange(Entity<GunHeatComponent> ent, ref OnTemperatureChangeEvent args)
    {
        Sync(ent, args.CurrentTemperature);

        if (args.CurrentTemperature >= ent.Comp.MeltTemperature)
            MeltFiringPin(ent);
    }

    private void Sync(Entity<GunHeatComponent> ent, float temperature, bool force = false)
    {
        if (!force && MathF.Abs(temperature - ent.Comp.Temperature) < SyncStep)
            return;

        // Clients derive the barrel glow from this, see the client GunHeatSystem.
        ent.Comp.Temperature = temperature;
        Dirty(ent);
    }

    private void MeltFiringPin(Entity<GunHeatComponent> ent)
    {
        if (!TryComp<FiringPinHolderComponent>(ent, out var holder) || holder.PinContainer.ContainedEntities.Count == 0)
            return;

        foreach (var pin in holder.PinContainer.ContainedEntities.ToArray())
        {
            QueueDel(pin);
        }

        if (ent.Comp.MeltedPin != null)
            SpawnNextToOrDrop(ent.Comp.MeltedPin.Value, ent);

        Audio.PlayPvs(ent.Comp.MeltSound, ent);
        Popup.PopupEntity(Loc.GetString("gun-heat-pin-melted", ("gun", ent.Owner)), ent, PopupType.MediumCaution);
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
            _temperature.ForceChangeTemperature(uid, temperature.CurrentTemperature - (excess * heat.PassiveCooling * seconds), temperature);
        }
    }
}
