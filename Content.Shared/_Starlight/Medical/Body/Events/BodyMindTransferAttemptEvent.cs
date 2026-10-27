namespace Content.Shared._Starlight.Medical.Body.Events;

/// <summary>
/// Raised first on the source and then on the target before an anatomy-driven mind transfer.
/// </summary>
/// <remarks>
/// Handlers may set <see cref="Cancelled"/> to protect either endpoint. This policy applies to organ insertion,
/// removal and the resulting Nymph transfer, not to general mind transfers such as mind swap spells.
/// </remarks>
/// <param name="Source">Entity currently holding the mind to be transferred.</param>
/// <param name="Target">Entity that would receive the mind.</param>
/// <param name="Cancelled">Whether a handler has rejected the transfer.</param>
[ByRefEvent]
public record struct BodyMindTransferAttemptEvent(EntityUid Source, EntityUid Target, bool Cancelled = false);
