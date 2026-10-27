using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Power.CombustionTurbine;

[RegisterComponent]
public sealed partial class CombustionTurbineRotorComponent : Component
{
    [ViewVariables]
    public EntityUid? Compressor;

    [ViewVariables]
    public EntityUid? Outlet;

    [ViewVariables]
    public CombustionTurbineAssembly Assembly = CombustionTurbineAssembly.MissingCompressor;

    /// <summary>
    /// Whether the turbine was switched on from the console.
    /// </summary>
    [DataField]
    public bool Enabled;

    [DataField]
    public float IntakeRatio = 0.5f;

    [DataField]
    public float MinIntakeRatio = 0.01f;

    /// <summary>
    /// Rotational energy of the rotor, in joules.
    /// </summary>
    [DataField]
    public float RotorEnergy;

    [DataField]
    public float RotorInertia = 0.4f;

    /// <summary>
    /// Litres per second the compressor draws at 100% intake, before the compressor part's multiplier.
    /// </summary>
    [DataField]
    public float IntakeVolume = 800f;

    /// <summary>
    /// Rotor speed at which the compressor reaches its full intake. Below it intake scales linearly.
    /// </summary>
    [DataField]
    public float IntakeFullRpm = 10000f;

    /// <summary>
    /// The blades turn tanh((T - 20 °C) / this) of the expansion into rotor work, so cold gas turns the turbine
    /// poorly and it pays to burn the mix first. Without it a volumetric intake would make as much power from cold
    /// gas as from hot gas at the same pressure.
    /// </summary>
    [DataField]
    public float ThermalScale = 1000f;

    /// <summary>
    /// Rotor work per joule of heat the gas loses while expanding, before the thermal factor.
    /// Keep <c>WorkMultiplier * tanh((593 K - 293 K) / ThermalScale)</c> below 1: thermomachine heaters reach
    /// 593 K, and above that a heater, a pump and this turbine in a loop would make free power.
    /// </summary>
    [DataField]
    public float WorkMultiplier = 2f;

    [DataField]
    public float MinOutletPressure = 1f;

    [DataField]
    public float FrictionCoefficient = 0.0033f;

    /// <summary>
    /// Extra friction from the rotor brake while the turbine is switched off, so it spins down in under a minute.
    /// </summary>
    [DataField]
    public float BrakeCoefficient = 0.08f;

    /// <summary>
    /// Below this speed a switched-off rotor counts as stopped, and its parts can be swapped.
    /// </summary>
    [DataField]
    public float StopRpm = 50f;

    /// <summary>
    /// Generator load: electrical power drawn from the rotor is this times its angular speed squared.
    /// </summary>
    [DataField]
    public float LoadCoefficient = 0.0326f;

    [DataField]
    public float PowerSmoothing = 0.3f;

    [DataField]
    public float RunningVisualThreshold = 1000f;

    [DataField]
    public float StarterRpm = 1000f;

    /// <summary>
    /// Once the starter has spun the rotor up, it only kicks in again below this speed.
    /// </summary>
    [DataField]
    public float StarterRestartRpm = 500f;

    [DataField]
    public float StarterPower = 3000f;

    /// <summary>
    /// Watts per second the starter draw grows by. APCs take a 1 kW step at once and ramp 500 W/s after that,
    /// so keep this below 500 or the starter browns out the room.
    /// </summary>
    [DataField]
    public float StarterRampRate = 400f;

    /// <summary>
    /// Current starter draw, in watts, on top of <see cref="IdlePower"/>.
    /// </summary>
    [ViewVariables]
    public float StarterLoad;

    [DataField]
    public float IdlePower = 200f;

    /// <summary>
    /// Rated speed, the average of the three installed parts.
    /// </summary>
    [ViewVariables]
    public float MaxRpm;

    /// <summary>
    /// Fraction of <see cref="MaxRpm"/> above which the governor starts closing the intake.
    /// </summary>
    [DataField]
    public float GovernorThreshold = 0.95f;

    /// <summary>
    /// How fast the governor closes the intake, per second, per unit of relative overspeed.
    /// </summary>
    [DataField]
    public float GovernorCloseRate = 10f;

    /// <summary>
    /// How fast the governor opens the intake back up, per second, while below its threshold.
    /// </summary>
    [DataField]
    public float GovernorOpenRate = 0.2f;

    /// <summary>
    /// Current governor throttle on the intake, 0–1. 1 means fully open.
    /// </summary>
    [DataField]
    public float GovernorFactor = 1f;

    /// <summary>
    /// Rated intake temperature, the average of the three installed parts.
    /// </summary>
    [ViewVariables]
    public float MaxTemperature;

    /// <summary>
    /// Intake volume multiplier from the compressor part.
    /// </summary>
    [ViewVariables]
    public float IntakeMultiplier = 1f;

    /// <summary>
    /// Friction multiplier from the rotor part.
    /// </summary>
    [ViewVariables]
    public float FrictionMultiplier = 1f;

    /// <summary>
    /// Isentropic efficiency of the expansion, from the stator part.
    /// </summary>
    [ViewVariables]
    public float ExpansionEfficiency;

    [DataField]
    public float Integrity = 100f;

    [DataField]
    public float OvertemperatureDamage = 1f;

    [DataField]
    public float MaxDamagePerSecond = 4f;

    [DataField]
    public float IntegrityRegen = 0.5f;

    /// <summary>
    /// Integrity below which engineering is warned over the radio.
    /// </summary>
    [DataField]
    public float AlertIntegrity = 90f;

    [DataField]
    public TimeSpan AlertCooldown = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan NextAlert;

    [DataField]
    public ProtoId<RadioChannelPrototype> AlertChannel = "Engineering";

    [DataField]
    public SoundSpecifier? AlertSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    /// <summary>
    /// Explosion intensity when the turbine fails, plus <see cref="ExplosionIntensityPerRpm"/> for its speed.
    /// </summary>
    [DataField]
    public float ExplosionIntensity = 50f;

    [DataField]
    public float ExplosionIntensityPerRpm = 0.004f;

    [DataField]
    public float ExplosionSlope = 4f;

    [DataField]
    public float ExplosionMaxTileIntensity = 20f;

    #region Last tick statistics, for the console
    [ViewVariables]
    public float Rpm;

    [ViewVariables]
    public float LastGeneration;

    [ViewVariables]
    public float InletTemperature;

    [ViewVariables]
    public float InletPressure;

    [ViewVariables]
    public float OutletPressure;

    [ViewVariables]
    public float ExhaustTemperature;

    [ViewVariables]
    public float MolesPerSecond;

    [ViewVariables]
    public bool StarterRunning;

    [ViewVariables]
    public bool Overheating;
    #endregion
}
