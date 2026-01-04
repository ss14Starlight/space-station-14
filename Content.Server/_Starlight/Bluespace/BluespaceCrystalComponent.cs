namespace Content.Server._Starlight.Bluespace;

[RegisterComponent]
public sealed partial class BluespaceCrystalComponent : Component
{
    /// <summary>
    /// Should teleport the target on use?
    /// </summary>
    [DataField]
    public bool Teleport = true;

    /// <summary>
    /// Teleport range.
    /// </summary>
    [DataField]
    public float Range = 8;

    /// <summary>
    /// Should shunt NullSpace ents?
    /// </summary>
    [DataField]
    public bool NullSpaceShunt = true;

    /// <summary>
    /// Nullspace shunt range.
    /// </summary>
    [DataField]
    public float NullSpaceShuntRange = 8;
}

[ByRefEvent]
public record struct NullSpaceShuntEvent();