namespace Content.Shared._Starlight.BodyControl;

/// <summary>Lets providers release their interfaces before a cooperative grant is removed.</summary>
[ByRefEvent]
public readonly record struct BodyControlStoppingEvent(EntityUid Controller, EntityUid Body);
