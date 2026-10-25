using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Starlight.Power.CombustionTurbine;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
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
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPowerReceiverSystem _receiver = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const float RpmPerRadian = 60f / (2f * MathF.PI);

    #region Simulation

    [SubscribeLocalEvent]
    private void OnRotorUpdate(Entity<CombustionTurbineRotorComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;
        var dt = args.dt;

        UpdateAssembly(ent);
        var ready = comp.Assembly == CombustionTurbineAssembly.Ready;
        if (!ready && comp.Enabled)
            comp.Enabled = false;

        var rpm = GetRpm(comp);
        UpdateStarter(ent, rpm, dt);

        GasMixture? inlet = null;
        GasMixture? outlet = null;
        if (ready && args.Grid is { } grid)
        {
            var inletTile = GetAdjacentTile(comp.Compressor!.Value, forward: false);
            var outletTile = GetAdjacentTile(comp.Outlet!.Value, forward: true);
            inlet = inletTile is { } i ? _atmosphere.GetTileMixture(grid, args.Map, i, true) : null;
            outlet = outletTile is { } o ? _atmosphere.GetTileMixture(grid, args.Map, o, true) : null;
        }

        comp.InletTemperature = inlet?.Temperature ?? 0f;
        comp.InletPressure = inlet?.Pressure ?? 0f;
        comp.OutletPressure = outlet?.Pressure ?? 0f;
        comp.MolesPerSecond = 0f;
        comp.ExhaustTemperature = 0f;

        var expansionWork = 0f;
        if (comp.Enabled && inlet != null && outlet != null && rpm > 0f)
            expansionWork = Expand(comp, inlet, outlet, rpm, dt);

        var maxOmega = comp.MaxRpm / RpmPerRadian;
        var maxEnergy = 0.5f * comp.RotorInertia * maxOmega * maxOmega;
        comp.RotorEnergy = Math.Clamp(comp.RotorEnergy + expansionWork, 0f, maxEnergy);

        var load = ready ? comp.LoadCoefficient : 0f;
        var friction = (comp.FrictionCoefficient * comp.FrictionMultiplier) + (comp.Enabled ? 0f : comp.BrakeCoefficient);
        var omegaSquared = 2f * comp.RotorEnergy / comp.RotorInertia;
        var drained = MathF.Min(comp.RotorEnergy, (load + friction) * omegaSquared * dt);
        var generated = load + friction > 0f
            ? drained * load / (load + friction)
            : 0f;
        comp.RotorEnergy -= drained;

        if (!comp.Enabled && GetRpm(comp) < comp.StopRpm)
            comp.RotorEnergy = 0f;

        comp.Rpm = GetRpm(comp);
        if (ready)
            UpdateGovernor(comp, dt);

        UpdateGenerator(ent, generated / dt);
        UpdateIntegrity(ent, dt);
    }

    private float Expand(CombustionTurbineRotorComponent comp, GasMixture inlet, GasMixture outlet, float rpm, float dt)
    {
        var inletPressure = inlet.Pressure;
        var outletPressure = MathF.Max(outlet.Pressure, comp.MinOutletPressure);
        if (inletPressure <= outletPressure)
            return 0f;

        var speed = MathF.Min(1f, rpm / comp.IntakeFullRpm);
        var volume = comp.IntakeVolume * comp.IntakeMultiplier * comp.IntakeRatio * comp.GovernorFactor * speed * dt;
        var passing = inlet.RemoveVolume(MathF.Min(volume, inlet.Volume));
        var moles = passing.TotalMoles;
        var temperature = passing.Temperature;
        var heatCapacity = _atmosphere.GetHeatCapacity(passing, true);
        var exponent = GetIsentropicExponent(passing);

        var work = 0f;
        if (moles > 0f && heatCapacity > 0f && exponent > 0f)
        {
            var idealDrop = temperature * (1f - MathF.Pow(outletPressure / inletPressure, exponent));
            var thermal = MathF.Tanh(MathF.Max(0f, temperature - Atmospherics.T20C) / comp.ThermalScale);
            var drop = MathF.Max(0f, MathF.Min(comp.ExpansionEfficiency * idealDrop, temperature - Atmospherics.TCMB));
            work = comp.WorkMultiplier * thermal * heatCapacity * drop;
            passing.Temperature = MathF.Max(Atmospherics.TCMB, temperature - drop);
        }

        comp.MolesPerSecond = moles / dt;
        comp.ExhaustTemperature = passing.Temperature;
        _atmosphere.Merge(outlet, passing);
        return work;
    }

    private float GetIsentropicExponent(GasMixture mixture)
    {
        var moles = mixture.TotalMoles;
        var heatCapacity = _atmosphere.GetHeatCapacity(mixture, false);
        return moles > 0f && heatCapacity > 0f ? Atmospherics.R * moles / heatCapacity : 0f;
    }

    private void UpdateStarter(Entity<CombustionTurbineRotorComponent> ent, float rpm, float dt)
    {
        var comp = ent.Comp;
        if (!comp.Enabled || rpm >= comp.StarterRpm)
            comp.StarterRunning = false;
        else if (rpm < comp.StarterRestartRpm)
            comp.StarterRunning = true;

        var load = comp.StarterRunning
            ? MathF.Min(comp.StarterPower, comp.StarterLoad + (comp.StarterRampRate * dt))
            : 0f;

        if (!MathHelper.CloseTo(load, comp.StarterLoad))
        {
            comp.StarterLoad = load;
            _receiver.SetLoad(ent.Owner, comp.IdlePower + load);
        }

        if (load > 0f && _receiver.IsPowered(ent.Owner))
            comp.RotorEnergy += load * dt;
    }

    private static void UpdateGovernor(CombustionTurbineRotorComponent comp, float dt)
    {
        var threshold = comp.MaxRpm * comp.GovernorThreshold;
        if (threshold <= 0f)
            return;

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

    private void UpdateGenerator(Entity<CombustionTurbineRotorComponent> ent, float power)
    {
        ent.Comp.LastGeneration = power;

        if (TryComp<PowerSupplierComponent>(ent, out var supplier))
            supplier.MaxSupply = (ent.Comp.PowerSmoothing * power) + ((1f - ent.Comp.PowerSmoothing) * supplier.MaxSupply);

        var running = power >= ent.Comp.RunningVisualThreshold;
        _ambient.SetAmbience(ent, running);
        _appearance.SetData(ent, CombustionTurbineVisuals.Running, running);

        if (ent.Comp.Compressor is { } compressor)
            _appearance.SetData(compressor, CombustionTurbineVisuals.Running, running);

        if (ent.Comp.Outlet is { } outlet)
            _appearance.SetData(outlet, CombustionTurbineVisuals.Running, running);
    }

    [SubscribeLocalEvent]
    private void OnRotorDisabled(Entity<CombustionTurbineRotorComponent> ent, ref AtmosDeviceDisabledEvent _)
    {
        ent.Comp.RotorEnergy = 0f;
        ent.Comp.Rpm = 0f;
        ent.Comp.StarterRunning = false;
        ent.Comp.StarterLoad = 0f;
        _receiver.SetLoad(ent.Owner, ent.Comp.IdlePower);

        UpdateGenerator(ent, 0f);
    }

    [SubscribeLocalEvent]
    private void OnRotorShutdown(Entity<CombustionTurbineRotorComponent> ent, ref ComponentShutdown _)
    {
        SetStage(ent, ref ent.Comp.Compressor, null);
        SetStage(ent, ref ent.Comp.Outlet, null);
    }

    #endregion

    #region Damage

    private void UpdateIntegrity(Entity<CombustionTurbineRotorComponent> ent, float dt)
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

    private void Fail(Entity<CombustionTurbineRotorComponent> ent)
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

    public void UpdateAssembly(Entity<CombustionTurbineRotorComponent> ent)
    {
        var comp = ent.Comp;
        Entity<CombustionTurbineStageComponent>? compressor = null;
        Entity<CombustionTurbineStageComponent>? outlet = null;

        var xform = Transform(ent);
        if (xform.Anchored && xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var dir = xform.LocalRotation.GetCardinalDir();
            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            compressor = FindStage((gridUid, grid), tile.Offset(dir.GetOpposite()), dir, CombustionTurbineStage.Compressor);
            outlet = FindStage((gridUid, grid), tile.Offset(dir), dir, CombustionTurbineStage.Outlet);
        }

        SetStage(ent, ref comp.Compressor, compressor);
        SetStage(ent, ref comp.Outlet, outlet);

        var compressorPart = compressor is { } c ? GetPart(c) : null;
        var rotorPart = TryComp<CombustionTurbineStageComponent>(ent, out var rotorStage) ? GetPart((ent, rotorStage)) : null;
        var statorPart = outlet is { } o ? GetPart(o) : null;

        comp.Assembly = (compressor, outlet, compressorPart, rotorPart, statorPart) switch
        {
            (null, _, _, _, _) => CombustionTurbineAssembly.MissingCompressor,
            (_, null, _, _, _) => CombustionTurbineAssembly.MissingOutlet,

            _ when IsPanelOpen(ent)
                || IsPanelOpen(compressor!.Value)
                || IsPanelOpen(outlet!.Value)
                => CombustionTurbineAssembly.PanelOpen,

            (_, _, null, _, _) => CombustionTurbineAssembly.MissingPart,
            (_, _, _, null, _) => CombustionTurbineAssembly.MissingPart,
            (_, _, _, _, null) => CombustionTurbineAssembly.MissingPart,

            _ => CombustionTurbineAssembly.Ready
        };

        if (comp.Assembly != CombustionTurbineAssembly.Ready)
        {
            comp.MaxRpm = 0f;
            comp.MaxTemperature = 0f;
            return;
        }

        comp.MaxRpm = (compressorPart!.GetMaxRpm() + rotorPart!.GetMaxRpm() + statorPart!.GetMaxRpm()) / 3f;
        comp.MaxTemperature = (compressorPart.GetMaxTemperature() + rotorPart.GetMaxTemperature() + statorPart.GetMaxTemperature()) / 3f;
        comp.IntakeMultiplier = compressorPart.GetEfficiency();
        comp.FrictionMultiplier = rotorPart.GetEfficiency();
        comp.ExpansionEfficiency = statorPart.GetEfficiency();
    }

    private Entity<CombustionTurbineStageComponent>? FindStage(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        Direction dir,
        CombustionTurbineStage stage)
    {
        var anchored = _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile);
        while (anchored.MoveNext(out var other))
        {
            if (!TryComp<CombustionTurbineStageComponent>(other, out var stageComp) || stageComp.Stage != stage)
                continue;

            if (Transform(other.Value).LocalRotation.GetCardinalDir() != dir)
                continue;

            return (other.Value, stageComp);
        }

        return null;
    }

    private void SetStage(
        Entity<CombustionTurbineRotorComponent> rotor,
        ref EntityUid? field,
        Entity<CombustionTurbineStageComponent>? stage)
    {
        var old = field;
        if (old == stage?.Owner)
            return;

        if (old is { } oldUid
            && !TerminatingOrDeleted(oldUid)
            && TryComp<CombustionTurbineStageComponent>(oldUid, out var oldStage)
            && oldStage.Rotor == rotor.Owner)
        {
            oldStage.Rotor = null;
            _appearance.SetData(oldUid, CombustionTurbineVisuals.Running, false);
        }

        field = stage?.Owner;
        if (stage is { } newStage)
            newStage.Comp.Rotor = rotor.Owner;
    }

    public Entity<CombustionTurbineRotorComponent>? GetRotor(EntityUid uid)
    {
        if (TryComp<CombustionTurbineRotorComponent>(uid, out var own))
            return (uid, own);

        if (!TryComp<CombustionTurbineStageComponent>(uid, out var stage)
            || stage.Rotor is not { } rotorUid
            || TerminatingOrDeleted(rotorUid)
            || !TryComp<CombustionTurbineRotorComponent>(rotorUid, out var rotor)
            || (rotor.Compressor != uid && rotor.Outlet != uid))
        {
            return null;
        }

        return (rotorUid, rotor);
    }

    private bool IsPanelOpen(EntityUid uid)
        => TryComp<WiresPanelComponent>(uid, out var panel) && panel.Open;

    private Vector2i? GetAdjacentTile(EntityUid uid, bool forward)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var dir = xform.LocalRotation.GetCardinalDir();
        return _map.TileIndicesFor(gridUid, grid, xform.Coordinates).Offset(forward ? dir : dir.GetOpposite());
    }

    private static float GetRpm(CombustionTurbineRotorComponent comp)
        => MathF.Sqrt(2f * comp.RotorEnergy / comp.RotorInertia) * RpmPerRadian;

    #endregion

    #region Examine

    [SubscribeLocalEvent]
    private void OnStageExamined(Entity<CombustionTurbineStageComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(CombustionTurbineStageComponent)))
        {
            if (GetPart(ent) is { } part)
                args.PushMarkup(Loc.GetString("combustion-turbine-examine-part", ("tier", part.Tier)));
            else
                args.PushMarkup(Loc.GetString("combustion-turbine-examine-no-part"));

            if (GetRotor(ent) is not { } rotor)
            {
                args.PushMarkup(Loc.GetString("combustion-turbine-examine-unassembled"));
                return;
            }

            if (rotor.Comp.Assembly != CombustionTurbineAssembly.Ready)
                args.PushMarkup(Loc.GetString(GetAssemblyLoc(rotor.Comp.Assembly)));

            if (ent.Comp.Stage != CombustionTurbineStage.Rotor)
                return;

            args.PushMarkup(Loc.GetString("combustion-turbine-examine-rpm", ("rpm", (int) rotor.Comp.Rpm)));
            args.PushMarkup(Loc.GetString("combustion-turbine-examine-integrity", ("integrity", (int) rotor.Comp.Integrity)));
            args.PushMarkup(Loc.GetString("combustion-turbine-examine-power", ("power", (int) rotor.Comp.LastGeneration)));
        }
    }

    public static string GetAssemblyLoc(CombustionTurbineAssembly assembly) => assembly switch
    {
        CombustionTurbineAssembly.Ready => "combustion-turbine-assembly-ready",
        CombustionTurbineAssembly.MissingCompressor => "combustion-turbine-assembly-missing-compressor",
        CombustionTurbineAssembly.MissingOutlet => "combustion-turbine-assembly-missing-outlet",
        CombustionTurbineAssembly.PanelOpen => "combustion-turbine-assembly-panel-open",
        _ => "combustion-turbine-assembly-missing-part",
    };

    #endregion
}
