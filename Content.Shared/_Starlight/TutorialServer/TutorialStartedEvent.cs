using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TutorialServer;

[Serializable, NetSerializable]
public sealed class TutorialStartedEvent : EntityEventArgs;
