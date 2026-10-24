namespace Content.Server._Starlight.Electrocution;

/// <summary>
/// Raised on an electrified entity after it successfully shocked <see cref="Target"/>.
/// Raised once per electrified act, not once per chained victim.
/// </summary>
[ByRefEvent]
public readonly record struct ElectrifiedShockDealtEvent(EntityUid Target);
