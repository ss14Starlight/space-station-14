using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Power.CombustionTurbine;

[RegisterComponent]
public sealed partial class CombustionTurbinePartComponent : Component
{
    /// <summary>
    /// The section this part fits in.
    /// </summary>
    [DataField(required: true)]
    public CombustionTurbineStage Stage;

    /// <summary>
    /// Current tier, starting at 1.
    /// </summary>
    [DataField]
    public int Tier = 1;

    /// <summary>
    /// Rotor speed the part is rated for, per tier. The turbine is limited by the average of its three parts.
    /// </summary>
    [DataField]
    public float[] MaxRpm = [30000f, 40000f, 52000f, 67000f];

    /// <summary>
    /// Intake gas temperature the part is rated for, per tier. The turbine is limited by the average of its three parts.
    /// </summary>
    [DataField]
    public float[] MaxTemperature = [8000f, 15000f, 30000f, 60000f];

    [DataField]
    public float[] Efficiency = [1f, 1f, 1f, 1f];

    /// <summary>
    /// Materials needed to reach the next tier; entry 0 upgrades tier 1 to tier 2.
    /// </summary>
    [DataField]
    public List<CombustionTurbinePartUpgrade> Upgrades = new();

    /// <summary>
    /// Seconds per current tier it takes to upgrade the part.
    /// </summary>
    [DataField]
    public float UpgradeDelayPerTier = 1f;

    public int MaxTier => MaxRpm.Length;

    public float GetMaxRpm() => GetTierValue(MaxRpm);

    public float GetMaxTemperature() => GetTierValue(MaxTemperature);

    public float GetEfficiency() => GetTierValue(Efficiency);

    public CombustionTurbinePartUpgrade? NextUpgrade
        => Tier >= MaxTier || Tier - 1 >= Upgrades.Count ? null : Upgrades[Tier - 1];

    private float GetTierValue(float[] values)
        => values.Length == 0 ? 0f : values[Math.Clamp(Tier - 1, 0, values.Length - 1)];
}

[DataDefinition]
public sealed partial class CombustionTurbinePartUpgrade
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> Stack;

    [DataField(required: true)]
    public int Amount;
}
