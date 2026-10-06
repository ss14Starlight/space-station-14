using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Actions.Events;

/// <summary>
/// Sent by a latch target's client when they press Struggle.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatchStruggleRequestEvent : EntityEventArgs
{
    /// <summary>
    /// How far past its stamped tick the client's frame was when the press
    /// happened, in seconds. The server clamps this to one tick.
    /// </summary>
    public float TickOffset;

    public LatchStruggleRequestEvent(float tickOffset) => TickOffset = tickOffset;
}
