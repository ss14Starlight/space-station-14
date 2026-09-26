using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Railroading.Events;

[Serializable, NetSerializable]
public sealed class OpenCardsRequestEvent : EntityEventArgs;
