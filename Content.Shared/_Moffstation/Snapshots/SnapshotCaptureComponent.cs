using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Snapshots;

/// <summary>
/// This component enables an item to record what its holder could see.
/// The scene is recorded server-side; see <see cref="SnapshotData"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SnapshotCaptureComponent : Component
{
    /// <summary>
    /// The entity that gets printed to hold the snapshot.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Snapshot;

    /// <summary>
    /// Tiles from the middle of the shot to its edge gives a 9x7 tile snapshot.
    /// </summary>
    [DataField]
    public Vector2i HalfExtents = new(4, 3);

    /// <summary>
    /// How far from the photographer the middle of a shot may be. Covers the screen with the viewfinder's extended view.
    /// </summary>
    [DataField]
    public float MaxRange = 14f;

    /// <summary>
    /// Whether the holder is looking through the viewfinder. While they are, clicking takes a snapshot centered on the
    /// clicked spot instead of interacting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Aiming;
}

/// <summary>
/// Raised on the capturing item when a shot has been taken, centered on <paramref name="Center"/>. The server records the snapshot.
/// </summary>
[ByRefEvent]
public readonly record struct SnapshotCapturedEvent(EntityUid User, MapCoordinates Center);
