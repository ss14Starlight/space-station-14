namespace Content.Server._Starlight.Electrocution;

/// <summary>
/// Raised on an electrified entity by <c>ElectrocutionSystem.TryDoElectrifiedAct</c> when it shocked
/// <see cref="Target"/>, the entity that touched it.
/// </summary>
/// <remarks>
/// <c>TryDoElectrifiedAct</c> shocks <see cref="Target"/> and then everyone chained to it by pulling.
/// The event is raised once, after that whole chain is processed, and only if <see cref="Target"/> itself was shocked.
/// Victims shocked through the pull chain don't raise it again.
/// </remarks>
[ByRefEvent]
public readonly record struct ElectrifiedShockDealtEvent(EntityUid Target);
