using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Starlight.Power.CombustionTurbine;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Power.CombustionTurbine;

public sealed partial class CombustionTurbineSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AmbientSoundSystem _ambient = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPowerReceiverSystem _receiver = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const float RpmPerRadian = 60f / (2f * MathF.PI);

    #region Simulation

    [SubscribeLocalEvent]
    private void OnCompressorUpdate(Entity<CombustionTurbineCompressorComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;
        var dt = args.dt;
        var generator = FindGenerator(ent);

        var rpm = GetRpm(comp);
        var powered = _receiver.IsPowered(ent.Owner);

        var starter = comp.Enabled && powered && generator != null && rpm < comp.StarterRpm;
        if (starter != comp.StarterRunning)
        {
            comp.StarterRunning = starter;
            _receiver.SetLoad(ent.Owner, starter ? comp.StarterPower : comp.IdlePower);
        }

        if (starter)
            comp.RotorEnergy += comp.StarterPower * dt;

        GasMixture? inlet = null;
        GasMixture? outlet = null;
        if (args.Grid is { } grid && generator is { } gen)
        {
            var inletTile = GetFacingTile(ent.Owner);
            var outletTile = GetFacingTile(gen.Owner);
            inlet = inletTile is { } i ? _atmosphere.GetTileMixture(grid, args.Map, i, true) : null;
            outlet = outletTile is { } o ? _atmosphere.GetTileMixture(grid, args.Map, o, true) : null;
        }

        comp.InletTemperature = inlet?.Temperature ?? 0f;
        comp.InletPressure = inlet?.Pressure ?? 0f;
        comp.OutletPressure = outlet?.Pressure ?? 0f;
        comp.MolesPerSecond = 0f;

        var compressionWork = 0f;
        var expansionWork = 0f;

        if (comp.Enabled && generator != null)
        {
            var speedup = _atmosphere.PumpSpeedup();

            if (inlet != null && rpm > 0f)
                compressionWork = Compress(comp, inlet, rpm, dt * speedup);

            if (outlet != null)
                expansionWork = Expand(comp, outlet, dt * speedup, dt);
        }

        var maxOmega = comp.MaxRpm / RpmPerRadian;
        var maxEnergy = 0.5f * comp.RotorInertia * maxOmega * maxOmega;
        comp.RotorEnergy = Math.Clamp(comp.RotorEnergy + expansionWork - compressionWork, 0f, maxEnergy);

        var load = generator?.Comp.LoadCoefficient ?? 0f;
        var omegaSquared = 2f * comp.RotorEnergy / comp.RotorInertia;
        var drained = MathF.Min(comp.RotorEnergy, (load + comp.FrictionCoefficient) * omegaSquared * dt);
        var generated = load + comp.FrictionCoefficient > 0f
            ? drained * load / (load + comp.FrictionCoefficient)
            : 0f;
        comp.RotorEnergy -= drained;

        comp.Rpm = GetRpm(comp);
        UpdateGovernor(comp, dt);

        if (generator is { } g)
            UpdateGenerator(g, generated / dt);

        UpdateIntegrity(ent, dt);
        UpdateCompressorVisuals(ent);
    }

    private float Compress(CombustionTurbineCompressorComponent comp, GasMixture inlet, float rpm, float dt)
    {
        var speed = MathF.Min(1f, rpm / comp.IntakeFullRpm);
        var volume = comp.IntakeVolume * comp.IntakeRatio * comp.GovernorFactor * speed * dt;
        var inletPressure = MathF.Max(inlet.Pressure, comp.MinOutletPressure);

        var removed = inlet.RemoveVolume(MathF.Min(volume, inlet.Volume));
        var moles = removed.TotalMoles;
        if (moles <= 0f)
            return 0f;

        var inletTemperature = removed.Temperature;
        _atmosphere.Merge(comp.Air, removed);

        var chamberPressure = comp.Air.Pressure;
        if (chamberPressure <= inletPressure)
            return 0f;

        var work = moles * Atmospherics.R * inletTemperature * MathF.Log(chamberPressure / inletPressure)
                   * comp.CompressionWorkFactor;
        _atmosphere.AddHeat(comp.Air, work);
        return work;
    }

    private float Expand(CombustionTurbineCompressorComponent comp, GasMixture outlet, float scaledDt, float dt)
    {
        var chamberPressure = comp.Air.Pressure;
        var outletPressure = MathF.Max(outlet.Pressure, comp.MinOutletPressure);
        if (comp.Air.TotalMoles <= 0f || chamberPressure <= outletPressure)
            return 0f;

        var passing = comp.Air.RemoveRatio(MathF.Min(1f, comp.OutflowRate * scaledDt));
        var moles = passing.TotalMoles;
        var temperature = passing.Temperature;
        var heatCapacity = _atmosphere.GetHeatCapacity(passing, true);

        var work = 0f;
        if (moles > 0f && heatCapacity > 0f)
        {
            var thermal = MathF.Tanh(MathF.Max(0f, temperature - Atmospherics.T20C) / comp.ThermalScale);
            work = moles * Atmospherics.R * temperature * MathF.Log(chamberPressure / outletPressure)
                   * comp.Efficiency * thermal;

            work = MathF.Min(work, heatCapacity * (temperature - Atmospherics.TCMB));
            passing.Temperature = MathF.Max(Atmospherics.TCMB, temperature - (work / heatCapacity));
        }

        comp.MolesPerSecond = moles / dt;
        _atmosphere.Merge(outlet, passing);
        return work;
    }

    private static void UpdateGovernor(CombustionTurbineCompressorComponent comp, float dt)
    {
        var threshold = comp.MaxRpm * comp.GovernorThreshold;
        if (comp.Rpm > threshold)
        {
            var excess = (comp.Rpm / threshold) - 1f;
            comp.GovernorFactor *= MathF.Max(0f, 1f - (comp.GovernorCloseRate * excess * dt));
            comp.GovernorFactor = MathF.Max(comp.GovernorFactor, 0.001f);
        }
        else
        {
            comp.GovernorFactor = MathF.Min(1f, comp.GovernorFactor + (comp.GovernorOpenRate * (1f - (comp.Rpm / threshold)) * dt));
        }
    }

    private void UpdateGenerator(Entity<CombustionTurbineGeneratorComponent> ent, float power)
    {
        ent.Comp.LastGeneration = power;

        if (TryComp<PowerSupplierComponent>(ent, out var supplier))
        {
            supplier.MaxSupply = (ent.Comp.PowerSmoothing * power)
                                 + ((1f - ent.Comp.PowerSmoothing) * supplier.MaxSupply);
        }

        var running = power >= ent.Comp.RunningVisualThreshold;
        _appearance.SetData(ent, CombustionTurbineVisuals.Running, running);
        _ambient.SetAmbience(ent, running);
    }

    private void UpdateCompressorVisuals(Entity<CombustionTurbineCompressorComponent> ent)
    {
        var rpm = ent.Comp.Rpm;
        var band = rpm switch
        {
            _ when rpm >= ent.Comp.MaxRpm * ent.Comp.GovernorThreshold => 4,
            > 10000f => 3,
            > 2000f => 2,
            > 500f => 1,
            _ => 0,
        };

        _appearance.SetData(ent, CombustionTurbineVisuals.Speed, band);
    }

    [SubscribeLocalEvent]
    private void OnCompressorDisabled(Entity<CombustionTurbineCompressorComponent> ent, ref AtmosDeviceDisabledEvent _)
    {
        ent.Comp.RotorEnergy = 0f;
        ent.Comp.Rpm = 0f;
        ent.Comp.StarterRunning = false;
        _receiver.SetLoad(ent.Owner, ent.Comp.IdlePower);

        if (_atmosphere.GetContainingMixture(ent.Owner, excite: true) is { } air)
            _atmosphere.Merge(air, ent.Comp.Air);
        ent.Comp.Air.Clear();

        if (ent.Comp.Generator is { } gen && TryComp<CombustionTurbineGeneratorComponent>(gen, out var genComp))
            UpdateGenerator((gen, genComp), 0f);

        UpdateCompressorVisuals(ent);
    }

    [SubscribeLocalEvent]
    private void OnCompressorShutdown(Entity<CombustionTurbineCompressorComponent> ent, ref ComponentShutdown _)
        => SetGenerator(ent, null);

    #endregion

    #region Damage

    private void UpdateIntegrity(Entity<CombustionTurbineCompressorComponent> ent, float dt)
    {
        var comp = ent.Comp;
        comp.Overheating = comp.Enabled && comp.InletTemperature > comp.MaxTemperature;
        var damage = 0f;
        if (comp.Overheating)
        {
            damage = MathF.Min(comp.MaxDamagePerSecond,
                comp.OvertemperatureDamage * (comp.InletTemperature - comp.MaxTemperature) / 1000f) * dt;
        }

        if (damage <= 0f)
        {
            comp.Integrity = MathF.Min(100f, comp.Integrity + (comp.IntegrityRegen * dt));
            return;
        }

        comp.Integrity = MathF.Max(0f, comp.Integrity - damage);

        if (comp.Integrity <= 0f)
        {
            Fail(ent);
            return;
        }

        if (comp.Integrity >= comp.AlertIntegrity || _timing.CurTime < comp.NextAlert)
            return;

        comp.NextAlert = _timing.CurTime + comp.AlertCooldown;
        var message = Loc.GetString("combustion-turbine-alert-damage",
            ("location", _navMap.GetNearestBeaconString(ent.Owner)),
            ("integrity", (int) MathF.Round(comp.Integrity)));
        _radio.SendRadioMessage(ent, message, comp.AlertChannel, ent);
        _audio.PlayPvs(comp.AlertSound, ent);
    }

    private void Fail(Entity<CombustionTurbineCompressorComponent> ent)
    {
        var comp = ent.Comp;
        var intensity = comp.ExplosionIntensity + (comp.Rpm * comp.ExplosionIntensityPerRpm);

        _adminLog.Add(LogType.Explosion, LogImpact.High,
            $"Combustion turbine {ToPrettyString(ent):entity} failed at {comp.Rpm:F0} RPM and exploded with intensity {intensity:F0}");

        _radio.SendRadioMessage(ent,
            Loc.GetString("combustion-turbine-alert-failure", ("location", _navMap.GetNearestBeaconString(ent.Owner))),
            comp.AlertChannel,
            ent);

        comp.Enabled = false;
        comp.RotorEnergy = 0f;
        comp.Rpm = 0f;

        _explosion.QueueExplosion(ent,
            ExplosionSystem.DefaultExplosionPrototypeId,
            intensity,
            comp.ExplosionSlope,
            comp.ExplosionMaxTileIntensity);
        QueueDel(ent);
    }

    #endregion

    #region Assembly

    public Entity<CombustionTurbineGeneratorComponent>? FindGenerator(Entity<CombustionTurbineCompressorComponent> ent)
    {
        var xform = Transform(ent);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return SetGenerator(ent, null);

        var dir = xform.LocalRotation.GetCardinalDir();
        var behind = _map.TileIndicesFor(gridUid, grid, xform.Coordinates).Offset(dir.GetOpposite());

        var anchored = _map.GetAnchoredEntities(gridUid, grid, behind);
        while (anchored.MoveNext(out var other))
        {
            if (!TryComp<CombustionTurbineGeneratorComponent>(other, out var generator))
                continue;

            if (Transform(other.Value).LocalRotation.GetCardinalDir() != dir.GetOpposite())
                continue;

            return SetGenerator(ent, (other.Value, generator));
        }

        return SetGenerator(ent, null);
    }

    private Entity<CombustionTurbineGeneratorComponent>? SetGenerator(
        Entity<CombustionTurbineCompressorComponent> ent,
        Entity<CombustionTurbineGeneratorComponent>? generator)
    {
        var old = ent.Comp.Generator;
        if (old == generator?.Owner)
            return generator;

        if (old is { } oldGen
            && !TerminatingOrDeleted(oldGen)
            && TryComp<CombustionTurbineGeneratorComponent>(oldGen, out var oldComp))
        {
            oldComp.Compressor = null;
            UpdateGenerator((oldGen, oldComp), 0f);
        }

        ent.Comp.Generator = generator?.Owner;
        if (generator is { } gen)
            gen.Comp.Compressor = ent.Owner;

        return generator;
    }

    private Vector2i? GetFacingTile(EntityUid uid)
    {
        var xform = Transform(uid);
        return xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid)
            ? null
            : _map.TileIndicesFor(gridUid, grid, xform.Coordinates).Offset(xform.LocalRotation.GetCardinalDir());
    }

    private static float GetRpm(CombustionTurbineCompressorComponent comp)
        => MathF.Sqrt(2f * comp.RotorEnergy / comp.RotorInertia) * RpmPerRadian;

    #endregion

    #region Examine

    [SubscribeLocalEvent]
    private void OnCompressorExamined(Entity<CombustionTurbineCompressorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(CombustionTurbineCompressorComponent)))
        {
            args.PushMarkup(Loc.GetString(ent.Comp.Generator == null
                ? "combustion-turbine-examine-no-generator"
                : "combustion-turbine-examine-connected"));
            args.PushMarkup(Loc.GetString("combustion-turbine-examine-rpm", ("rpm", (int) ent.Comp.Rpm)));
            args.PushMarkup(Loc.GetString("combustion-turbine-examine-integrity", ("integrity", (int) ent.Comp.Integrity)));
        }
    }

    [SubscribeLocalEvent]
    private void OnGeneratorExamined(Entity<CombustionTurbineGeneratorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(CombustionTurbineGeneratorComponent)))
        {
            if (ent.Comp.Compressor == null)
                args.PushMarkup(Loc.GetString("combustion-turbine-examine-no-compressor"));

            args.PushMarkup(Loc.GetString("combustion-turbine-examine-power", ("power", (int) ent.Comp.LastGeneration)));
        }
    }

    #endregion
}
