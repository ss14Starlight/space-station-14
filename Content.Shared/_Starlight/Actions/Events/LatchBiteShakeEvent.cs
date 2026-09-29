using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Actions.Events;

/// <summary>
/// Tells clients to play the K9's brief head-shake animation on a Bite Harder use.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatchBiteShakeEvent : EntityEventArgs
{
    public NetEntity Latcher;

    public LatchBiteShakeEvent(NetEntity latcher) => Latcher = latcher;
}
