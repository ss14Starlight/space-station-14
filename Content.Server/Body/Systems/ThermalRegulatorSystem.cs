using Content.Server.Body.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Temperature.Components;
using Robust.Shared.Timing;

#region Starlight
using Content.Shared.Mobs.Systems;
#endregion Starlight

namespace Content.Server.Body.Systems;

public sealed class ThermalRegulatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly TemperatureSystem _tempSys = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSys = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;  // Starlight edit

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalRegulatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ThermalRegulatorComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    private void OnMapInit(Entity<ThermalRegulatorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _gameTiming.CurTime + ent.Comp.UpdateInterval;
    }

    private void OnUnpaused(Entity<ThermalRegulatorComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.NextUpdate += args.PausedTime;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ThermalRegulatorComponent>();
        while (query.MoveNext(out var uid, out var regulator))
        {
            if (_gameTiming.CurTime < regulator.NextUpdate)
                continue;

            regulator.NextUpdate += regulator.UpdateInterval;
            ProcessThermalRegulation((uid, regulator));
        }
    }

    /// <summary>
    /// Processes thermal regulation for a mob
    /// </summary>
    private void ProcessThermalRegulation(Entity<ThermalRegulatorComponent, TemperatureComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp2, logMissing: false))
            return;

        // TODO: Why do we have two datafields for this if they are only ever used once here?
        var totalMetabolismTempChange = ent.Comp1.MetabolismHeat - ent.Comp1.RadiatedHeat;

        // Starlight edit start - Don't do implicit heat regulation if the entity is dead
        // Fixes Avali not rotting
        var heatCapacity = _tempSys.GetHeatCapacity(ent, ent);
        if (!_mobState.IsDead(ent))
        {
            // implicit heat regulation
            var implicitTempDiff = Math.Abs(ent.Comp2.CurrentTemperature - ent.Comp1.NormalBodyTemperature);
            var implicitTargetHeat = implicitTempDiff * heatCapacity;
            if (ent.Comp2.CurrentTemperature > ent.Comp1.NormalBodyTemperature)
            {
                totalMetabolismTempChange -= Math.Min(implicitTargetHeat, ent.Comp1.ImplicitHeatRegulation);
            }
            else
            {
                totalMetabolismTempChange += Math.Min(implicitTargetHeat, ent.Comp1.ImplicitHeatRegulation);
            }
        }
        // Starlight edit end

        _tempSys.ChangeHeat(ent, totalMetabolismTempChange, ignoreHeatResistance: true, ent);

        // recalc difference and target heat
        // Starlight edit start
        var tempDiff = Math.Abs(ent.Comp2.CurrentTemperature - ent.Comp1.NormalBodyTemperature);
        var targetHeat = tempDiff * heatCapacity;
        // Starlight edit end

        // if body temperature is not within comfortable, thermal regulation
        // processes starts
        if (tempDiff < ent.Comp1.ThermalRegulationTemperatureThreshold)
            return;

        if (ent.Comp2.CurrentTemperature > ent.Comp1.NormalBodyTemperature)
        {
            if (!_actionBlockerSys.CanSweat(ent))
                return;

            _tempSys.ChangeHeat(ent, -Math.Min(targetHeat, ent.Comp1.SweatHeatRegulation), ignoreHeatResistance: true, ent);
        }
        else
        {
            if (!_actionBlockerSys.CanShiver(ent))
                return;

            _tempSys.ChangeHeat(ent, Math.Min(targetHeat, ent.Comp1.ShiveringHeatRegulation), ignoreHeatResistance: true, ent);
        }
    }
}
