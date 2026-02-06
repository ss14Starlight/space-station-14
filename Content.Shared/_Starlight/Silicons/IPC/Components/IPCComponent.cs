// IPC Component - Marker component for IPC entities
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

namespace Content.Shared._Starlight.Silicons.IPC.Components;

/// <summary>
/// Marker component that identifies an entity as an IPC (Integrated Positronic Chassis).
/// IPCs are robotic humanoids with special mechanics:
/// - Cannot sleep (robots don't need biological rest)
/// - Die from overheating (circuit failure)
/// - Use custom death messages (servo/circuit descriptions)
/// - Can be healed with cables/welders instead of medicine
/// - Take increased shock damage
/// </summary>
/// <remarks>
/// This is intentionally a marker component with no data.
/// All IPC-specific data lives in other components (IPCBattery, IPCRevive, etc.).
/// Systems check for this component to apply IPC-only logic.
/// </remarks>
[RegisterComponent]
public sealed partial class IPCComponent : Component
{
    // Marker component - no fields needed
    // All IPC data is in specialized components:
    // - IPCBatteryComponent: Power/charging
    // - IPCReviveComponent: Reboot mechanics
    // - KillOnOverheatComponent: Temperature death
    // - HumanoidEMPComponent: EMP effects
}

