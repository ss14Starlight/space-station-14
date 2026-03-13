using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Body.Systems;
using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Server._FarHorizons.Vehicles.Atmos;

/// <summary>
/// Handles atmospheric systems for mechs including air circulation, fans, and life support.
/// </summary>
public sealed class VehicleAtmosphereSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    private const float MinExternalPressure = 0.05f;
    private const float PressureTolerance = 0.1f;

    /// <inheritdoc/>
    public override void Initialize()
    {        
        SubscribeLocalEvent<VehicleModsComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<VehicleEquipmentComponent, VehicleFanToggle>(OnFanToggle);

        SubscribeLocalEvent<RiderComponent, InhaleLocationEvent>(OnInhale);
        SubscribeLocalEvent<RiderComponent, ExhaleLocationEvent>(OnExhale);
        SubscribeLocalEvent<RiderComponent, AtmosExposedGetAirEvent>(OnExpose);
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
        if(ent.Comp.Equipment[EquipmentType.AIRTANK] == null) 
            return false;
        if (!TryComp<GasTankComponent>(ent.Comp.Equipment[EquipmentType.AIRTANK], out var tank))
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

    private void OnInhale(Entity<RiderComponent> ent, ref InhaleLocationEvent args)
    {
        if(ent.Comp.Riding == null) return;
        if (!HasComp<VehicleComponent>(ent.Comp.Riding))
            return;
        if (TryComp<VehicleCabinAirComponent>(ent.Comp.Riding, out var cabin))
        {
            var vol = args.Respirator.BreathVolume;
            var breath = new GasMixture(vol)
            {
                Temperature = cabin.Air.Temperature
            };
            var pressure = cabin.RegulatorPressure;
            _atmosphere.PumpGasTo(cabin.Air, breath, pressure);
            args.Gas = breath;
            return;
        }
        args.Gas = _atmosphere.GetContainingMixture(ent.Comp.Riding.Value, excite: true);
    }

    private void OnExhale(Entity<RiderComponent> ent, ref ExhaleLocationEvent args)
    {
        if(ent.Comp.Riding == null) return;
        if (!TryComp<VehicleComponent>(ent.Comp.Riding, out var vehicleComp))
            return;

        args.Gas = GetBreathMixture((ent.Comp.Riding.Value, vehicleComp));
    }

    private void OnExpose(Entity<RiderComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        if(ent.Comp.Riding == null) return;
        if (!TryComp<VehicleComponent>(ent.Comp.Riding, out var vehicleComp))
            return;

        args.Gas = GetBreathMixture((ent.Comp.Riding.Value, vehicleComp), args.Excite);
        args.Handled = true;
    }

    private GasMixture? GetBreathMixture(Entity<VehicleComponent> ent, bool excite = true)
    {
        if (TryComp<VehicleCabinAirComponent>(ent.Owner, out var cabin))
            return cabin.Air;

        return _atmosphere.GetContainingMixture(ent.Owner, excite: excite);
    }

    #endregion

    #region Fan

    private bool UpdateFanModule(Entity<VehicleModsComponent> ent, float frameTime)
    {
        var fanModule = GetFanModule(ent);
        if (fanModule == null || !fanModule.IsActive)
        {
            if (fanModule != null)
                SetFanState(ent, fanModule, FanState.Off);

            return false;
        }

        var (tankComp, tankAir) = GetGasTank(ent);
        if (tankAir == null || tankComp == null)
        {
            SetFanState(ent, fanModule, FanState.Off);
            return false;
        }

        return ProcessFanOperation(ent, fanModule, tankComp, tankAir, frameTime);
    }

    private (GasTankComponent? tank, GasMixture? air) GetGasTank(Entity<VehicleModsComponent> ent)
    {
        if (TryComp<GasTankComponent>(ent.Comp.Equipment[EquipmentType.AIRTANK], out var tank))
                return (tank, tank.Air);

        return (null, null);
    }

    private bool ProcessFanOperation(Entity<VehicleModsComponent> ent,
        VehicleFanModComponent fanModule,
        GasTankComponent tankComp,
        GasMixture tankAir,
        float frameTime)
    {        
        var external = _atmosphere.GetContainingMixture(ent.Owner);

        var transferVolume = fanModule.GasProcessingRate * frameTime;

        if (transferVolume <= 0)
            return false;

        if(TryComp<VehicleCabinAirComponent>(ent.Owner, out var cabin) && cabin.Air != null)
        {
            var removedInternal = cabin.Air.RemoveVolume(transferVolume);
            {
                if(fanModule is { FilterGases.Count: > 0 })
                {
                    var filtered = new GasMixture { Temperature = removedInternal.Temperature };
                    _atmosphere.ScrubInto(removedInternal, filtered, fanModule.FilterGases);

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
            SetFanState(ent, fanModule, FanState.Idle);
            return false;
        }

        var success = ProcessFilteredTransfer(external, tankAir, fanModule, transferVolume);
        SetFanState(ent, fanModule, success ? FanState.On : FanState.Idle);
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

        if (fanModule is { FilterGases.Count: > 0 })
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

    public void SetFanState(Entity<VehicleModsComponent> ent, VehicleFanModComponent fanModule, FanState state)
    {
        if (fanModule.State == state)
            return;

        fanModule.State = state;
        Dirty(ent);
    }

    private void OnFanToggle(Entity<VehicleEquipmentComponent> ent, ref VehicleFanToggle args)
    {
        var fanModule = GetFanModule(ent);
        if (fanModule == null || args.Handled)
            return;
        fanModule.IsActive = !fanModule.IsActive;

        // Set the correct state based on the toggle.
        var newState = fanModule.IsActive ? FanState.On : FanState.Off;
        if (fanModule.State != newState)
        {
            fanModule.State = newState;
            Dirty(ent);
        }
        args.Handled = true;
    }
    
    private VehicleFanModComponent? GetFanModule(EntityUid ent)
    {
        // Direct component case
        if (TryComp<VehicleFanModComponent>(ent, out var fanModule))
            return fanModule;

        // Equipment case
        if (!TryComp<VehicleModsComponent>(ent, out var vmComp))
            return null;

        if (!vmComp.Equipment.TryGetValue(EquipmentType.VENTFAN, out var fanUid))
            return null;

        if (!TryComp(fanUid, out fanModule))
            return null;

        return fanModule;
    }

    #endregion
}