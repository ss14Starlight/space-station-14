using Robust.Shared.GameStates;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class MovementBodyPartComponent : Component
{
    [DataField("walkSpeed")]
    public float WalkSpeed = MovementSpeedModifierComponent.DefaultBaseWalkSpeed;

    [DataField("sprintSpeed")]
    public float SprintSpeed = MovementSpeedModifierComponent.DefaultBaseSprintSpeed;

    [DataField("acceleration")]
    public float Acceleration = MovementSpeedModifierComponent.DefaultAcceleration;

    // 🌟Starlight🌟 Start
    /// <summary>
    /// The density this leg can effectively move, it’s a temporary solution until we implement proper weight calculations for all body parts.
    /// </summary>
    [DataField]
    public float MaxDensity = 92.5f;

    /// <summary>
    /// The minimum speed this leg is allowed to move.
    /// </summary>
    [DataField]
    public float MinSpeedMod = 0.0f;

    /// <summary>
    /// The maximum speed this leg is allowed to move
    /// </summary>
    [DataField]
    public float MaxSpeedMod = 20.0f;

    // 🌟Starlight🌟 End
}
