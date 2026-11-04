using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Snapshots;

[Serializable, NetSerializable]
public enum SnapshotViewUiKey : byte
{
    Key,
}

/// <summary>
/// Requests the Snapshot once the UI is opened
/// </summary>
[Serializable, NetSerializable]
public sealed class SnapshotViewRequestMessage : BoundUserInterfaceMessage;

/// <remarks>
/// This is deliberately not UI state so only the viewer gets it, and not a UI message either otherwise
/// they get dropped and add a delay of about ~400ms
/// </remarks>
[Serializable, NetSerializable]
public sealed class SnapshotViewDataEvent(NetEntity snapshot, SnapshotData data) : EntityEventArgs
{
    public readonly NetEntity Snapshot = snapshot;
    public readonly SnapshotData Data = data;
}
