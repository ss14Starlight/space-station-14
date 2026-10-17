using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Power.CombustionTurbine;

[Serializable, NetSerializable]
public enum CombustionTurbineVisuals : byte
{
    Speed,

    Running,
}

[Serializable, NetSerializable]
public enum CombustionTurbineVisualLayers : byte
{
    Speed,
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
    /// A compressor is linked and has its generator half next to it.
    /// </summary>
    public bool Connected;

    /// <summary>
    /// A compressor is linked but the generator half is missing.
    /// </summary>
    public bool GeneratorMissing;

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
    public float ChamberPressure;
    public float OutletPressure;
    public float MaxTemperature;
    public float MolesPerSecond;
    public float IntakeRatio;

    /// <summary>
    /// Litres per second at 100% intake.
    /// </summary>
    public float IntakeVolume;
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
