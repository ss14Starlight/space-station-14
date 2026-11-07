using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Atmos.Piping.Unary.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Atmos.Piping.Binary.Systems;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.DeviceLinking;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

public sealed partial class AirlockCyclerSystem
{
    [Dependency] private GasVentScrubberSystem _scrubbers = default!;
    [Dependency] private SharedGasPressurePumpSystem _pressurePumps = default!;
    [Dependency] private SharedGasVolumePumpSystem _volumePumps = default!;

    [Dependency] private EntityQuery<GasVentScrubberComponent> _scrubberQuery = default!;
    [Dependency] private EntityQuery<GasPressurePumpComponent> _pressurePumpQuery = default!;
    [Dependency] private EntityQuery<GasVolumePumpComponent> _volumePumpQuery = default!;
    [Dependency] private EntityQuery<AirlockCyclerDeviceComponent> _deviceQuery = default!;

    private bool IsDevicePort(AirlockCyclerComponent comp, string port)
        => port == comp.InletPort || port == comp.OutletPort;

    private bool IsInletDevice(EntityUid uid)
        => _ventQuery.HasComp(uid);

    private bool IsOutletDevice(EntityUid uid)
        => _ventQuery.HasComp(uid)
        || _scrubberQuery.HasComp(uid)
        || _pressurePumpQuery.HasComp(uid)
        || _volumePumpQuery.HasComp(uid);

    private bool IsTileDevice(EntityUid uid)
        => _ventQuery.HasComp(uid) || _scrubberQuery.HasComp(uid);

    private void DriveDevices(Layout layout, bool pressurizing, float target)
    {
        foreach (var inlet in layout.Inlets)
        {
            if (pressurizing)
                _vents.SetAirlockCyclerMode(inlet, true, VentPumpDirection.Releasing, target);
            else if (!layout.Outlets.Contains(inlet))
                StopDevice(inlet);
        }

        foreach (var outlet in layout.Outlets)
        {
            if (!pressurizing)
                DriveOutlet(outlet, target);
            else if (!layout.Inlets.Contains(outlet))
                StopDevice(outlet);
        }
    }

    private void DriveOutlet(EntityUid uid, float target)
    {
        if (_ventQuery.HasComp(uid))
            _vents.SetAirlockCyclerMode(uid, true, VentPumpDirection.Siphoning, target);
        else if (_scrubberQuery.HasComp(uid))
            _scrubbers.SetAirlockCyclerMode(uid, true);
        else
            SetPump(uid, true);
    }

    private void StopDevices(Entity<AirlockCyclerComponent> ent, DeviceLinkSourceComponent source)
    {
        foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.InletPort))
            StopDevice(linked);

        foreach (var linked in GetOwnedSinks(ent, source, ent.Comp.OutletPort))
            StopDevice(linked);
    }

    private void StopDevice(EntityUid uid)
    {
        // Takes control of a linked device that was never driven: left on its own settings, an inlet vent would
        // keep filling the chamber while the outlets empty it.
        if (_ventQuery.HasComp(uid))
            _vents.StopAirlockCyclerVent(uid);
        else if (_scrubberQuery.HasComp(uid))
            _scrubbers.StopAirlockCyclerScrubber(uid);
        else
            SetPump(uid, false);
    }

    private void ReleaseDevice(EntityUid uid)
    {
        if (!_deviceQuery.TryComp(uid, out var saved))
            return;

        if (_ventQuery.HasComp(uid))
        {
            _vents.ReleaseAirlockCyclerControl(uid);
            return;
        }

        if (_scrubberQuery.HasComp(uid))
        {
            _scrubbers.ReleaseAirlockCyclerControl(uid);
            return;
        }

        var enabled = saved.Enabled;
        RemComp<AirlockCyclerDeviceComponent>(uid);
        if (_pressurePumpQuery.TryComp(uid, out var pressure))
            _pressurePumps.Set(uid, pressure, enabled);
        else if (_volumePumpQuery.TryComp(uid, out var volume))
            _volumePumps.Set(uid, volume, enabled);
    }

    private void SetPump(EntityUid uid, bool enabled)
    {
        if (_pressurePumpQuery.TryComp(uid, out var pressure))
        {
            EnsureSaved(uid, pressure.Enabled);
            _pressurePumps.Set(uid, pressure, enabled);
        }
        else if (_volumePumpQuery.TryComp(uid, out var volume))
        {
            EnsureSaved(uid, volume.Enabled);
            _volumePumps.Set(uid, volume, enabled);
        }
    }

    private void EnsureSaved(EntityUid uid, bool enabled)
    {
        if (_deviceQuery.HasComp(uid))
            return;

        AddComp<AirlockCyclerDeviceComponent>(uid).Enabled = enabled;
    }
}
