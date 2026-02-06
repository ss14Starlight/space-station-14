// IPC System - Main (Server)
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Server.DoAfter;
using Content.Shared._Starlight.Silicons.IPC;

namespace Content.Server._Starlight.Silicons.IPC;

/// <summary>
/// Main IPC system handling server-side logic for Integrated Positronic Chassis.
/// This is a partial class split across multiple files:
/// - IPCSystem.Battery.cs: Power management, battery drain, death timers
/// - IPCSystem.Revive.cs: Reboot mechanics, defib interaction, revival system
/// - IPCSystem.Temperature.cs: Overheat shutdown and temperature effects
/// - IPCSystem.Ui.cs: User interface handling
/// </summary>
public sealed partial class IPCSystem : SharedIPCSystem 
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;

    /// <summary>
    /// Initializes all subsystems of the IPC system.
    /// Called once when the system starts up.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();        // Calls SharedIPCSystem.Initialize() which calls SetupBattery()
        SetupRevive();            // _STARLIGHT: Initialize reboot/defib system
        InitializeTemperature();  // _STARLIGHT: Initialize overheat shutdown system
    }
}

