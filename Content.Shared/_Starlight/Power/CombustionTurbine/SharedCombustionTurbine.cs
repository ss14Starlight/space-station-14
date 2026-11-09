using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Power.CombustionTurbine;

[Serializable, NetSerializable]
public enum CombustionTurbineStage : byte
{
    Compressor,
    Rotor,
    Outlet,
}

[Serializable, NetSerializable]
public enum CombustionTurbineAssembly : byte
{
    Ready,
    MissingCompressor,
    MissingOutlet,
    PanelOpen,
    MissingPart,
}

[Serializable, NetSerializable]
public enum CombustionTurbineVisuals : byte
{
    Running,
}

[Serializable, NetSerializable]
public enum CombustionTurbineVisualLayers : byte
{
    Running,
}

[Serializable, NetSerializable]
public enum CombustionTurbineConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CombustionTurbineConsoleState : BoundUserInterfaceState
{
    /// <summary>
    /// A core rotor is linked to the console.
    /// </summary>
    public bool Linked;

    public CombustionTurbineAssembly Assembly = CombustionTurbineAssembly.MissingCompressor;

    public bool Enabled;
    public bool StarterRunning;

    /// <summary>
    /// How far the speed governor has opened the intake, 0–1. Below 1 it is holding the rotor back.
    /// </summary>
    public float Governor = 1f;

    public bool Overheating;
    public float Rpm;
    public float MaxRpm;
    public float Power;
    public float Integrity;
    public float InletTemperature;
    public float InletPressure;
    public float OutletPressure;
    public float ExhaustTemperature;
    public float MaxTemperature;
    public float MolesPerSecond;
    public float IntakeRatio;

    /// <summary>
    /// Litres per second at 100% intake, with the compressor part's bonus.
    /// </summary>
    public float IntakeVolume;

    public int[] PartTiers = new int[3];
}

[Serializable, NetSerializable]
public sealed class CombustionTurbineToggleMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class CombustionTurbineIntakeMessage(float ratio) : BoundUserInterfaceMessage
{
    public readonly float Ratio = ratio;
}

[Serializable, NetSerializable]
public sealed partial class CombustionTurbinePartPryDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CombustionTurbinePartUpgradeDoAfterEvent : SimpleDoAfterEvent;
