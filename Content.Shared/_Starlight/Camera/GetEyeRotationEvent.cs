namespace Content.Shared._Starlight.Camera;

/// <summary>
///     Raised directed by-ref when <see cref="SharedContentEyeSystem.UpdateEyeRotation"/> is called.
///     Should be subscribed to by any systems that want to modify an entity's eye rotation,
///     so that they do not override each other.
/// </summary>
/// <remarks>
///     Counterpart of <see cref="GetEyeOffsetEvent"/>, but for rotation, to use for screenshake.
/// </remarks>
[ByRefEvent]
public record struct GetEyeRotationEvent(Angle Rotation);