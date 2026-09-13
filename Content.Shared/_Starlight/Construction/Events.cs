using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Construction;

/// <summary>
/// Fired when a user attempts to interact with something, triggering a construction step.
/// </summary>
[ByRefEvent]
public record struct ConstructionInteractAttemptEvent(EntityUid User, EntityUid Target, bool Canceled = false);

/// <summary>
/// Sent client -> server to instantly finish every construction ghost the client has placed.
/// </summary>
[Serializable, NetSerializable]
public sealed class DebugFinishConstructionGhostsMessage : EntityEventArgs
{
    public readonly List<DebugConstructionGhost> Ghosts;

    public DebugFinishConstructionGhostsMessage(List<DebugConstructionGhost> ghosts)
    {
        Ghosts = ghosts;
    }
}

/// <summary>
/// A single construction ghost in a <see cref="DebugFinishConstructionGhostsMessage"/>.
/// </summary>
[Serializable, NetSerializable]
public struct DebugConstructionGhost
{
    public NetCoordinates Location;
    public string PrototypeName;
    public Angle Angle;

    /// <summary>
    /// Identifier to be sent back in the acknowledgement so that the client can clean up its ghost.
    /// </summary>
    public int Ack;
}
