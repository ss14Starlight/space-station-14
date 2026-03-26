using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Movement.Components;

/// <summary>
///     Movement Speed Scale: changes the effectiveness of movement modifiers to be closer to the original value
///     Lower values will cause increases or decreases in speed to effect less.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MovementSpeedModifierScaleComponent : Component
{
    /// <summary>
    ///     TODO: Change Summary - Sets the scale that all speed modifiers are scaled by if used with ModifySpeedScaled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementSpeedScale = 1f;
}