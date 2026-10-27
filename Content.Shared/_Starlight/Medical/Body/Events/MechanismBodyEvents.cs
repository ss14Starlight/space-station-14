namespace Content.Shared._Starlight.Medical.Body.Events;

// All of these events are raised on a mechanism entity when added/removed to a body in different
// ways.

/// <summary>
/// Raised on a mechanism when it is added to a body part.
/// </summary>
[ByRefEvent]
public readonly record struct OrganAddedEvent(EntityUid Part);

/// <summary>
/// Raised on an organ when it becomes part of a body, including when its containing limb is attached.
/// </summary>
/// <remarks>
/// Persistent organ effects should follow this event rather than completion of a particular surgery step.
/// </remarks>
/// <param name="Body">Body receiving the organ.</param>
/// <param name="Part">Part containing the organ.</param>
/// <param name="IsImplanted">False for cavity storage, which must not activate implant effects.</param>
[ByRefEvent]
public readonly record struct OrganAddedToBodyEvent(EntityUid Body, EntityUid Part, bool IsImplanted = true);

/// <summary>
/// Raised on a mechanism when it is removed from a body part.
/// </summary>
[ByRefEvent]
public readonly record struct OrganRemovedEvent(EntityUid OldPart);

/// <summary>
/// Raised on an organ when it leaves a body, including when its containing limb is detached.
/// </summary>
/// <remarks>
/// The organ may remain inside a detached part. Use the supplied former body when undoing its effects;
/// these notifications also occur during entity deletion.
/// </remarks>
/// <param name="OldBody">Body that previously contained the organ.</param>
/// <param name="OldPart">Part containing the organ before the change.</param>
/// <param name="IsImplanted">False for cavity storage, which did not activate implant effects.</param>
[ByRefEvent]
public readonly record struct OrganRemovedFromBodyEvent(EntityUid OldBody, EntityUid OldPart, bool IsImplanted = true);
