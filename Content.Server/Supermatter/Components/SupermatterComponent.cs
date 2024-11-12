using Content.Shared.Atmos;
using Robust.Shared.Audio;

namespace Content.Server.Supermatter.Components;

[RegisterComponent]
public sealed partial class SupermatterComponent : Component
{
    /// <summary>
    ///     The damage taken from direct hits, e.g. laser weapons
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float AVExternalDamage = 0f;

    //
    [ViewVariables(VVAccess.ReadOnly)]
    public float HeatAccumulatorRate = 0.840f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float AVHeatAccumulator = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float RadiationAccumulatorRate = 0.11f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float AVRadiationAccumulator = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float LightingAccumulatorThreshold = 1.5f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float LightingAccumulatorRate = 0.015f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float AVLightingAccumulator = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float InternalEnergyAccumulatorRate = 0.035f;
    /// <summary>
    ///     Lightning prototype IDs that the supermatter should spit out.
    /// </summary>
    public readonly string[] LightningPrototypeIDs =
    {
        "Lightning",
        "ChargedLightning",
        "SuperchargedLightning",
        "HyperchargedLightning"
    };
    public readonly string SliverPrototype = "SupermatterSliver";

    [DataField("zapSound")]
    public static SoundSpecifier SupermatterZapSound = new SoundPathSpecifier("/Audio/Weapons/emitter2.ogg");

    [DataField("calmAmbienceSound")]
    public SoundSpecifier CalmAmbienceSound = new SoundPathSpecifier("/Audio/Ambience/Objects/supermatter_calm.ogg");

    [DataField("delamAmbienceSound")]
    public SoundSpecifier DelamAmbienceSound = new SoundPathSpecifier("/Audio/Ambience/Objects/supermatter_delam.ogg");

    [ViewVariables]
    public SoundSpecifier CurrentAmbience = new SoundPathSpecifier("/Audio/Ambience/Objects/supermatter_calm.ogg");

    [DataField("vaporizeSound")]
    public static SoundSpecifier VaporizeSound = new SoundPathSpecifier("/Audio/Effects/Grenades/Supermatter/supermatter_start.ogg");

    [DataField("teslaSpawnPrototype")]
    public string TeslaPrototype = "TeslaEnergyBall";

    [DataField("singularitySpawnPrototype")]
    public string SingularityPrototype = "Singularity";

    [DataField("supermatterKudzuSpawnPrototype")]
    public string SupermatterKudzuPrototype = "SupermatterKudzu";

    /// <summary>
    ///     If a supermatter sliver has been removed. Lowers the delamination countdown time.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool SliverRemoved = false;

    /// <summary>
    ///     Indicates whether supermatter crystal is active or not.
    /// </summary>
    [DataField("activated")]
    public bool Activated = false;

    [ViewVariables]
    public GasMixture AbsorbedGasMix = new();

    /// <summary>
    ///     Delta time between Update() calls storage.
    /// </summary>
    public float DeltaTime = 0f;

    public float UpdateTimerAccumulator = 0f;

    public float AnnouncementTimerAccumulator = 0f;

    /// <summary>
    ///     Amount of seconds to pass before another SM cycle.
    /// </summary>
    [DataField("updateTimer")]
    public float UpdateTimer = 1f;

    /// <summary>
    ///     Amount of seconds to pass before makes an announcement.
    /// </summary>
    [DataField("announcementTimer")]
    public float AnnouncementTimer = 60f;

    [ViewVariables(VVAccess.ReadWrite)]
    public int? PreferredDelamType = 0;

    /// <summary>
    ///     The time in seconds for crystal to delaminate.
    /// </summary>
    [DataField("countdownTimer")]
    public float DelamCountdownTimerRaw = 45f;
    public float DelamCountdownTimer => SliverRemoved ? DelamCountdownTimerRaw / 2 : DelamCountdownTimerRaw;
    public bool DelamAnnouncementHappened = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DelamCountdownAccumulator = 0f;

    /// <summary>
    ///     The portion of gasmix we should absorb.
    /// </summary>
    [DataField("gasAbsorptionRatio")]
    public float AbsorptionRatio = .15f;

    /// <summary>
    ///     This value effects gas output, damage and power generation.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float InternalEnergy = 0f;

    /// <summary>
    ///     The amount of damage the SM currently has.
    /// </summary>
    [DataField("damage")]
    public float Damage = 0f;

    /// <summary>
    ///     The amount of damage SM had before the cycle.
    /// </summary>
    public float DamageArchive = 0f;

    [DataField("damageDangerPoint")]
    public float DamageDangerPoint = 50f;

    [DataField("damageEmergencyPoint")]
    public float DamageEmergencyPoint = 75f;

    [DataField("damageDelaminationPoint")]
    public float DelaminationPoint = 95f;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool AreWeDelaming = false;

    /// <summary>
    ///     Affects the heat SM makes.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float StabilizationMinimum = 0f;
    /// <summary>
    ///     Affters the minimum point at which SM takes damage.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float RadiationAbsorption = 0f;
    /// <summary>
    ///     How much power decay is negated. Complete power decay negation at 1.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float GasPowerlossInhibition = 0f;
    [ViewVariables(VVAccess.ReadOnly)]
    public float ThermalСonductivity = 0f;
    /// <summary>
    ///     Affects the power gain the SM experiences from heat.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float HeatPowerGeneration = 0f;

    /// <summary>
    ///     Lesser than that and it's not worth processing.
    /// </summary>
    public const float MinimumMoleCount = .01f;

    public const float HeatPenaltyThreshold = 100f;
    public const float PowerPenaltyThreshold = 3f;
    public const float MolePenaltyMinThreshold = 15f;
    public const float MolePenaltyMaxThreshold = 900f;
    public const float PresureMinPenaltyThreshold = 10;
    public const float PresureMaxPenaltyThreshold = 100;
    public const float ThermalReleaseModifier = 4f;
    public const float PlasmaReleaseModifier = 1.5f;
    public const float OxygenReleaseModifier = 6.5f;
    public const float GasHeatPowerScaling = 1f / 6f;

    /// <summary>
    ///     Stores gas properties used for the supermatter.
    ///     Array values should alwayd match gas enum values.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public GasFact[] GasFacts =
    {
        new (thermalConductivity: 2.2f, radiationAbsorption: 2.00f, stabilizationMinimum: -0.4f, heatPowerGeneration: 1f), // o2
        new (thermalConductivity: 2.0f, radiationAbsorption: 2.10f, stabilizationMinimum: -0.3f, heatPowerGeneration: -1), // n2
        new (thermalConductivity: 1.2f, radiationAbsorption: 3.50f, stabilizationMinimum: -0.3f, heatPowerGeneration: 1f, powerlossInhibition: 1f), // co2
        new (thermalConductivity: 6.0f, radiationAbsorption: 1.45f, stabilizationMinimum: -0.4f, heatPowerGeneration: 1f), // plasma
        new (thermalConductivity: 3.0f, radiationAbsorption: 1.30f, stabilizationMinimum: -0.5f, heatPowerGeneration: 1f), // tritium
        new (thermalConductivity: 1.4f, radiationAbsorption: 4.00f, stabilizationMinimum: -0.2f, heatPowerGeneration: 1f), // vapor
        new (thermalConductivity: 1.6f, radiationAbsorption: 2.80f, stabilizationMinimum: -0.1f,heatPowerGeneration: .5f), // ommonium
        new (thermalConductivity: 1.3f, radiationAbsorption: 2.30f, stabilizationMinimum: -0.1f), // n2o
        new (thermalConductivity: 9.9f, radiationAbsorption: 1.15f, stabilizationMinimum: -0.8f, heatPowerGeneration: 1f), // frezon
    };
}

/// <summary>
///     Stores gas properties used for the supermatter.
/// </summary>
[Serializable]
[DataDefinition]
public sealed partial class GasFact
{
    /// <summary>
    ///     Affects the amount of power the main SM zap makes.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float ThermalСonductivity;
    /// <summary>
    ///     Affects the heat SM makes.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float StabilizationMinimum;
    /// <summary>
    ///     Affters the minimum point at which SM takes damage.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float RadiationAbsorption;
    /// <summary>
    ///     Affects the power gain the SM experiences from heat.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float HeatPowerGeneration;
    /// <summary>
    ///     How much power decay is negated. Complete power decay negation at 1.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float PowerlossInhibition;

    public GasFact(float? thermalConductivity = null, float? radiationAbsorption = null, float? stabilizationMinimum = null, float? heatPowerGeneration = null, float? powerlossInhibition = null)
    {
        ThermalСonductivity = thermalConductivity ?? 1;
        StabilizationMinimum = stabilizationMinimum ?? 1;
        RadiationAbsorption = radiationAbsorption ?? 1;
        HeatPowerGeneration = heatPowerGeneration ?? 0;
        PowerlossInhibition = powerlossInhibition ?? 0;
    }
}

/// <summary>
///     Type of delamination that should occur.
/// </summary>
public enum DelamType : sbyte
{
    Explosion = 0,
    Tesla = 1,
    Singularity = 2,
    ResonanceCascade = 3,
}
