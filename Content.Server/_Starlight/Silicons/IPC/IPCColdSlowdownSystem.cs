// IPC Cold Slowdown System
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Server.Temperature.Systems;
using Content.Shared._Starlight.Silicons.IPC.Components;
using Content.Shared.Alert;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;

namespace Content.Server._Starlight.Silicons.IPC;

public sealed class IPCColdSlowdownSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private const short MaxColdAlertLevel = 3; // Maximum cold alert severity

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IPCComponent, OnTemperatureChangeEvent>(OnTemperatureChanged);
    }

    /// <summary>
    /// Adds or removes IPCColdSlowedComponent based on temperature alert level.
    /// </summary>
    private void OnTemperatureChanged(EntityUid uid, IPCComponent component, OnTemperatureChangeEvent args)
    {
        if (!TryComp<TemperatureComponent>(uid, out var temp))
            return;

        // Check if at maximum cold alert level
        // We need to get the alert category from the alert prototype
        var alertKey = AlertKey.ForCategory(TemperatureSystem.TemperatureAlertCategory);
        var atMaxCold = _alerts.TryGetAlertState(uid, alertKey, out var alertState) 
                        && alertState.Severity >= MaxColdAlertLevel;

        // Add or remove component as needed
        if (atMaxCold && !HasComp<IPCColdSlowedComponent>(uid))
        {
            AddComp<IPCColdSlowedComponent>(uid);
        }
        else if (!atMaxCold && HasComp<IPCColdSlowedComponent>(uid))
        {
            RemComp<IPCColdSlowedComponent>(uid);
        }
    }
}

