namespace Content.Shared._Starlight.Cybernetics.Components;

/// <summary>
/// Applies stamina damage when colliding with an entity.
/// </summary>
[RegisterComponent]
public sealed partial class CyberneticDisruptionOnCollideComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    [DataField]
    public float DisableChance = 1.0f;
}
