namespace Content.Server._Starlight.Atmos.Components;

[RegisterComponent]
public sealed partial class InlineHeatExchangerComponent : Component
{
    /// <summary>
    /// Pipenet to act on.
    /// </summary>
    [DataField]
    public string NodeName = "pipe";

    /// <summary>
    /// Thermal convection coefficient (J/degK/sec).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("convectionCoefficient")]
    public float K { get; set; } = 8000f;

    /// <summary>
    /// Thermal radiation coefficient.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("radiationCoefficient")]
    public float alpha { get; set; } = 140f;
}
