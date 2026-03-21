using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Cybernetics.Components;

/// <summary>
/// Hitscan entities that have this component will deal stamina damage to the target.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanCyberneticDisruptionComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    [DataField]
    public float DisableChance = 1.0f;
}
