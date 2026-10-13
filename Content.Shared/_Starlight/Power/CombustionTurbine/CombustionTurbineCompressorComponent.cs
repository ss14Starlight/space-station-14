using Content.Shared.Atmos;
using Content.Shared.DeviceLinking;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Power.CombustionTurbine;

[RegisterComponent]
public sealed partial class CombustionTurbineCompressorComponent : Component
{
    /// <summary>
    /// The generator half, found on the tile behind the compressor.
    /// </summary>
    [ViewVariables]
    public EntityUid? Generator;

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
    /// Gas held between the compressor and the turbine.
    /// </summary>
    [DataField]
    public GasMixture Air = new(1000f) { Temperature = Atmospherics.T20C };

    /// <summary>
    /// Rotational energy of the rotor, in joules.
    /// </summary>
    [DataField]
    public float RotorEnergy;

    [DataField]
    public float RotorInertia = 0.4f;

    [DataField]
    public float IntakeVolume = 400f;

    /// <summary>
    /// Rotor speed at which the compressor reaches its full intake. Below it intake scales linearly.
    /// </summary>
    [DataField]
    public float IntakeFullRpm = 10000f;

    /// <summary>
    /// Fraction of the internal chamber pushed through the turbine per second.
    /// </summary>
    [DataField]
    public float OutflowRate = 2f;

    [DataField]
    public float Efficiency = 0.35f;

    /// <summary>
    /// Expansion work is scaled by tanh((T - 20 °C) / this), so cold gas turns the turbine poorly
    /// and it pays to burn the mix first.
    /// </summary>
    [DataField]
    public float ThermalScale = 1000f;

    [DataField]
    public float CompressionWorkFactor = 0.5f;

    [DataField]
    public float MinOutletPressure = 1f;

    [DataField]
    public float FrictionCoefficient = 0.0018f;

    [DataField]
    public float StarterRpm = 1000f;

    [DataField]
    public float StarterPower = 3000f;

    [DataField]
    public float IdlePower = 200f;

    [DataField]
    public float MaxRpm = 50000f;

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

    [DataField]
    public float MaxTemperature = 8000f;

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
    public float ExplosionIntensityPerRpm = 0.006f;

    [DataField]
    public float ExplosionSlope = 4f;

    [DataField]
    public float ExplosionMaxTileIntensity = 20f;

    [DataField]
    public ProtoId<SinkPortPrototype> ConsolePort = "CombustionTurbineConsoleReceiver";

    #region Last tick statistics, for the console
    [ViewVariables]
    public float Rpm;

    [ViewVariables]
    public float InletTemperature;

    [ViewVariables]
    public float InletPressure;

    [ViewVariables]
    public float OutletPressure;

    [ViewVariables]
    public float MolesPerSecond;

    [ViewVariables]
    public bool StarterRunning;

    [ViewVariables]
    public bool Overheating;
    #endregion
}
