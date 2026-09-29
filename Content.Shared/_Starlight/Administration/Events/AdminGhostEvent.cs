using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Administration.Events;

/// Sent to client when they become an admin observer.
[Serializable, NetSerializable]
public sealed class AdminGhostEvent : EntityEventArgs
{
}
