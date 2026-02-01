namespace Content.Server._Starlight.Antags.Vampires.Components;

/// <summary>
/// Handles the simple runtime behaviour for vampire decoy entities.
/// </summary>
[RegisterComponent]
public sealed partial class VampireDecoyComponent : Component
{
    /// <summary>
    /// Ensures the flash/explosion only happens once.
    /// </summary>
    public bool Detonated;
}
