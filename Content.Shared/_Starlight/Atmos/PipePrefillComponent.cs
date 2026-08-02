using Content.Shared.Atmos;

namespace Content.Shared._Starlight.Atmos;

/// <summary>
/// Prefills a singular pipenet node with the specified mixture and parameters.
/// Note that this is additive with multiple <see cref="PipePrefillComponent"/>s that are connected to the same
/// resulting pipenet.
/// </summary>
[RegisterComponent]
public sealed partial class PipePrefillComponent : Component
{
    /// <summary>
    /// Fraction of each gas in the final mixture. Numbers are relative to one another.
    /// </summary>
    [DataField(required: true)] public Dictionary<Gas, float> Mixture = new();

    /// <summary>
    /// The temperature that the added mixture should be at.
    /// </summary>
    [DataField] public float Temperature = Atmospherics.T20C;

    /// <summary>
    /// Target total amount of mols to have at <see cref="Temperature"/>.
    /// </summary>
    [DataField] public float? TargetMoles;

    /// <summary>
    /// Target pressure, in kPa, at <see cref="Temperature"/>.
    /// </summary>
    [DataField] public float? TargetPressure;
}
