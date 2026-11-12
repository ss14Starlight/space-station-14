using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Snapshots;

/// <summary>
/// Bound-UI key for the snapshot viewer.
/// </summary>
[Serializable, NetSerializable]
public enum SnapshotViewUiKey : byte
{
    Key,
}

/// <summary>
/// Requests the snapshot data once the view UI is open.
/// </summary>
[Serializable, NetSerializable]
public sealed class SnapshotViewRequestMessage : BoundUserInterfaceMessage;

/// <summary>
/// Snapshot payload sent to the single viewing player.
/// </summary>
/// <remarks>
/// Deliberately not UI state (which would go to everyone with the UI open) and not a UI message
/// (which would be dropped and add ~400 ms delay). See <c>ServerSnapshotViewSystem</c>.
/// </remarks>
[Serializable, NetSerializable]
public sealed class SnapshotViewDataEvent(NetEntity snapshot, SnapshotData data) : EntityEventArgs
{
    public readonly NetEntity Snapshot = snapshot;
    public readonly SnapshotData Data = data;
}

