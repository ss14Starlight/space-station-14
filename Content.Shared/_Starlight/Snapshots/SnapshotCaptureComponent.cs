using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Snapshots;

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
    /// Tiles from the frame center to its edge. The default (4, 3) gives a 9x7 frame.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2i HalfExtents = new(4, 3);

    /// <summary>
    /// Available total frame dimensions, in the order shown by the zoom verb. Both dimensions must be positive odd numbers.
    /// </summary>
    [DataField]
    public List<Vector2i> ZoomLevels = [];

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

    /// <summary>
    /// Shutter sound played on every successful shot.
    /// </summary>
    [DataField]
    public SoundSpecifier ShutterSound = new SoundPathSpecifier("/Audio/Misc/camera_snap.ogg");

    /// <summary>
    /// Short-lived white light spawned at the camera while its flash fires, so the photograph records the burst.
    /// </summary>
    [DataField]
    public EntProtoId FlashEffect = "EffectSnapshotFlash";
}

/// <summary>
/// Raised on the capturing item when a shot has been taken. The server records the snapshot and prints it.
/// </summary>
/// <param name="User">The photographer.</param>
/// <param name="Center">Frame center in map coordinates.</param>
/// <param name="Rotation">Eye rotation for the shot.</param>
/// <param name="AnimationFrames">Client-captured animation frames, if any.</param>
/// <param name="IconSmoothStates">Client-captured icon-smoothing states, if any.</param>
/// <param name="Focus">Entity clicked through the viewfinder, if any, used for subject examine text.</param>
[ByRefEvent]
public readonly record struct SnapshotCapturedEvent(
    EntityUid User,
    MapCoordinates Center,
    Angle Rotation,
    List<SnapshotSpriteAnimation>? AnimationFrames,
    List<SnapshotSpriteLayerStateCapture>? IconSmoothStates,
    EntityUid? Focus);
