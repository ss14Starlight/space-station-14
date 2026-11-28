using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Body.Components;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Vehicle;
using Content.Shared.Body.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Containers;
using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Server._Starlight.Vehicle.Atmos;

/// <summary>
/// Handles atmospheric systems for mechs including air circulation, fans, and life support.
/// </summary>
public sealed class VehicleAtmosphereSystem : EntitySystem
{
    [Dependency] private VehicleSystem _vehicles = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    private const float MinExternalPressure = 0.05f;
    private const float PressureTolerance = 0.1f;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleModsComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<VehicleEquipmentComponent, VehicleFanToggle>(OnFanToggle);

        SubscribeLocalEvent<RespiratorComponent, InhaleLocationEvent>(OnInhale);
        SubscribeLocalEvent<RespiratorComponent, ExhaleLocationEvent>(OnExhale);
        SubscribeLocalEvent<BodyComponent, AtmosExposedGetAirEvent>(OnExpose);
    }

    private void OnAtmosUpdate(Entity<VehicleModsComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        UpdateFanModule(ent, args.dt);
        UpdateCabinPressure(ent);
    }

    #region Cabin

    public bool TryGetGasModuleAir(Entity<VehicleModsComponent> ent, out GasMixture? air)
    {
        air = null;
        if (!ent.Comp.Equipment.TryGetValue(EquipmentType.AIRTANK, out var part) ||
            CompOrNull<VehicleEquipmentComponent>(part)?.Broken == true ||
            !TryComp<GasTankComponent>(part, out var tank))
            return false;

        air = tank.Air;
        return true;
    }

    private bool UpdateCabinPressure(Entity<VehicleModsComponent> ent)
    {
        if (!TryComp<VehicleCabinAirComponent>(ent.Owner, out var cabin))
            return false;

        if (!TryGetGasModuleAir(ent, out var tankAir) || tankAir == null)
            return false;

        return _atmosphere.PumpGasTo(tankAir, cabin.Air, cabin.TargetPressure);
    }

    private void OnInhale(Entity<RespiratorComponent> ent, ref InhaleLocationEvent args)
    {
        if (args.Gas != null || GetCabinVehicle(ent.Owner) is not { } vehicle)
            return;

        if (!TryComp<VehicleCabinAirComponent>(vehicle, out var cabin))
        {
            args.Gas = _atmosphere.GetContainingMixture(vehicle, excite: true);
            return;
        }

        var breath = new GasMixture(args.Respirator.BreathVolume) { Temperature = cabin.Air.Temperature };
        _atmosphere.PumpGasTo(cabin.Air, breath, cabin.RegulatorPressure);
        args.Gas = breath;
    }

    private void OnExhale(Entity<RespiratorComponent> ent, ref ExhaleLocationEvent args)
    {
        if (args.Gas == null && GetCabinVehicle(ent.Owner) is { } vehicle)
            args.Gas = GetBreathMixture(vehicle);
    }

    private void OnExpose(Entity<BodyComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled || GetCabinVehicle(ent.Owner) is not { } vehicle)
            return;

        args.Gas = GetBreathMixture(vehicle, args.Excite);
        args.Handled = true;
    }

    private EntityUid? GetCabinVehicle(EntityUid occupant)
    {
        return _vehicles.GetOccupiedVehicle(occupant) is { } vehicle &&
            HasComp<VehiclePassengerSeatsComponent>(vehicle) ? vehicle : null;
    }

    private GasMixture? GetBreathMixture(EntityUid vehicle, bool excite = true)
        => CompOrNull<VehicleCabinAirComponent>(vehicle)?.Air ?? _atmosphere.GetContainingMixture(vehicle, excite: excite);

    #endregion

    #region Fan

    private bool UpdateFanModule(Entity<VehicleModsComponent> ent, float frameTime)
    {
        var fanModule = GetFanModule(ent.Comp);
        if (fanModule == null || !fanModule.Value.Comp.IsActive)
        {
            if (fanModule != null)
                SetFanState(fanModule.Value, FanState.Off);

            return false;
        }

        var (tankComp, tankAir) = GetGasTank(ent);
        if (tankAir == null || tankComp == null)
        {
            SetFanState(fanModule.Value, FanState.Off);
            return false;
        }

        return ProcessFanOperation(ent, fanModule.Value, tankComp, tankAir, frameTime);
    }

    private (GasTankComponent? tank, GasMixture? air) GetGasTank(Entity<VehicleModsComponent> ent)
    {
        if (ent.Comp.Equipment.TryGetValue(EquipmentType.AIRTANK, out var part) &&
            CompOrNull<VehicleEquipmentComponent>(part)?.Broken != true &&
            TryComp<GasTankComponent>(part, out var tank))
                return (tank, tank.Air);

        return (null, null);
    }

    private bool ProcessFanOperation(Entity<VehicleModsComponent> ent,
        Entity<VehicleFanModComponent> fanModule,
        GasTankComponent tankComp,
        GasMixture tankAir,
        float frameTime)
    {
        var external = _atmosphere.GetContainingMixture(ent.Owner);

        var transferVolume = fanModule.Comp.GasProcessingRate * frameTime;

        if (transferVolume <= 0)
            return false;

        if(TryComp<VehicleCabinAirComponent>(ent.Owner, out var cabin) && cabin.Air != null)
        {
            var removedInternal = cabin.Air.RemoveVolume(transferVolume);
            {
                if(fanModule.Comp.FilterGases.Count > 0)
                {
                    var filtered = new GasMixture { Temperature = removedInternal.Temperature };
                    _atmosphere.ScrubInto(removedInternal, filtered, fanModule.Comp.FilterGases);

                    if(external != null)
                        _atmosphere.Merge(external, filtered);
                    else
                        filtered.Clear();
                }
            }
            _atmosphere.Merge(cabin.Air, removedInternal);
        }

        if (external == null
            || external.Pressure <= MinExternalPressure
            || tankAir.Pressure >= tankComp.MaxOutputPressure - PressureTolerance)
        {
            SetFanState(fanModule, FanState.Idle);
            return false;
        }

        var success = ProcessFilteredTransfer(external, tankAir, fanModule.Comp, transferVolume);
        SetFanState(fanModule, success ? FanState.On : FanState.Idle);
        return success;
    }

    private bool ProcessFilteredTransfer(GasMixture external,
        GasMixture tankAir,
        VehicleFanModComponent fanModule,
        float transferVolume)
    {
        // Calculate transfer volume based on processing rate.
        if (transferVolume <= 0)
            return false;

        // Remove gas from external environment.
        var removed = external.RemoveVolume(transferVolume);
        if (removed.TotalMoles <= 0)
            return false;

        if (fanModule.FilterGases.Count > 0)
        {
            var filtered = new GasMixture { Temperature = removed.Temperature };
            _atmosphere.ScrubInto(removed, filtered, fanModule.FilterGases);
            // Return filtered gases to external environment.
            _atmosphere.Merge(external, filtered);
        }


        // Add remaining gas to internal tank (either unfiltered, or post-scrub remainder).
        _atmosphere.Merge(tankAir, removed);
        return true;
    }

    public void SetFanState(Entity<VehicleFanModComponent> fan, FanState state)
    {
        var active = state != FanState.Off;
        if (fan.Comp.State == state && fan.Comp.IsActive == active)
            return;

        fan.Comp.State = state;
        fan.Comp.IsActive = active;
        Dirty(fan);
    }

    private void OnFanToggle(EntityUid uid, VehicleEquipmentComponent part, VehicleFanToggle args)
    {
        if (args.Handled || part.Broken || !TryComp<VehicleFanModComponent>(uid, out var fan) ||
            !_containers.TryGetContainingContainer(uid, out var container) ||
            _vehicles.GetOperatorOrNull(container.Owner) != args.Performer ||
            CompOrNull<VehicleIgnitionComponent>(container.Owner)?.Started != true)
            return;

        SetFanState((uid, fan), fan.IsActive ? FanState.Off : FanState.On);
        args.Handled = true;
    }

    private Entity<VehicleFanModComponent>? GetFanModule(VehicleModsComponent mods)
    {
        if (!mods.Equipment.TryGetValue(EquipmentType.VENTFAN, out var uid) || uid is not { } part ||
            CompOrNull<VehicleEquipmentComponent>(part)?.Broken == true || !TryComp<VehicleFanModComponent>(part, out var fan))
            return null;

        return (part, fan);
    }

    #endregion
}
