using System.Numerics;
using Content.Shared.DisplacementMap;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.MapText;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Snapshots;

/// <summary>
/// Viewers rebuild this on a client-only map, so clients never upload image data.
/// </summary>
/// <remarks>
/// Positions and rotations are relative to the camera-aligned snapshot frame, centered on the middle tile.
/// </remarks>
[Serializable, NetSerializable]
public sealed class SnapshotData
{

    /// <summary>
    /// Tiles from the frame center to its edge.
    /// </summary>
    public Vector2i HalfExtents;

    /// <summary>
    /// Map ambient light for unroofed tiles. Roof colors are stored per tile.
    /// </summary>
    public Color AmbientLight = Color.Black;

    /// <summary>
    /// The map's space background, or null for the default one.
    /// </summary>
    public string? Parallax;

    /// <summary>
    /// World position of the middle of the frame. The snapshot is rebuilt there so the background lines up.
    /// </summary>
    public Vector2 WorldCenter;

    /// <summary>
    /// Tiles captured in the frame, as offsets from the center tile.
    /// </summary>
    public List<SnapshotTile> Tiles = [];

    /// <summary>
    /// Entities captured in the frame, in capture order.
    /// </summary>
    public List<SnapshotEntity> Entities = [];

    /// <summary>
    /// Tiles in the frame the photographer couldn't see. They're drawn black; otherwise they would show the space background.
    /// </summary>
    public List<Vector2i> Hidden = [];

    /// <summary>
    /// Where the photographer stood, relative to the middle of the frame. Shadows are cast from here.
    /// </summary>
    public Vector2 Viewer;

    /// <summary>
    /// Tiles with something blocking sight (walls, closed doors) between the photographer and the frame, as offsets from
    /// the middle tile.
    /// </summary>
    public List<Vector2i> Occluders = [];

    /// <summary>
    /// Name text for the subject clicked through the viewfinder, if that entity was included in the shot.
    /// </summary>
    public string? SubjectNameText;

    /// <summary>
    /// Examine text for the subject, if any.
    /// </summary>
    public string? SubjectDescription;
}

/// <summary>
/// One tile captured in a snapshot frame.
/// </summary>
/// <param name="Offset">Tile offset from the frame center.</param>
/// <param name="TileId">Tile prototype ID.</param>
/// <param name="Variant">Tile variant.</param>
/// <param name="RotationMirroring">Tile rotation/mirroring flags.</param>
/// <param name="Roof">Roof color if the tile is roofed; roofs keep the map's ambient light off indoor tiles.</param>
[Serializable, NetSerializable]
public record struct SnapshotTile(
    Vector2i Offset,
    string TileId,
    byte Variant,
    byte RotationMirroring,
    Color? Roof);

/// <summary>
/// One entity captured in a snapshot frame.
/// </summary>
[Serializable, NetSerializable]
public sealed class SnapshotEntity
{
    /// <summary>
    /// Prototype to respawn when rebuilding.
    /// </summary>
    public EntProtoId Prototype;

    /// <summary>
    /// Position relative to the frame center.
    /// </summary>
    public Vector2 Offset;

    /// <summary>
    /// Frame-relative rotation (world rotation plus frame rotation), applied as local rotation on rebuild.
    /// </summary>
    public Angle Rotation;

    /// <summary>
    /// Whether the entity was anchored.
    /// </summary>
    public bool Anchored;

    /// <summary>
    /// This entity is outside the frame and only exists to provide nearby illumination.
    /// </summary>
    public bool SupportOnly;

    /// <summary>
    /// Whether the character showed as SSD.
    /// </summary>
    public bool Ssd;

    /// <summary>
    /// Copy of the entity's networked appearance so the rebuilt view matches.
    /// </summary>
    public Dictionary<Enum, object>? Appearance;

    /// <summary>
    /// Animation frame indices captured by the photographer for sprite layers.
    /// </summary>
    public List<SnapshotSpriteLayerFrame>? AnimationFrames;

    /// <summary>
    /// Captured icon-smoothing states, so cropped neighbors cannot change the photographed sprite.
    /// </summary>
    public List<SnapshotSpriteLayerState>? IconSmoothStates;

    /// <summary>
    /// Captured light state, if the entity emitted light.
    /// </summary>
    public SnapshotLight? Light;

    /// <summary>
    /// Map text set on the entity itself (e.g. dev map signs), which respawning the prototype would lose.
    /// </summary>
    public MapTextComponentState? MapText;

    /// <summary>
    /// Index in <see cref="SnapshotData.Entities"/> of the entity this one is buckled to, so draw order is preserved (e.g. a rider draws on top).
    /// </summary>
    public int? BuckledTo;

    /// <summary>
    /// Humanoid appearance data, when the entity is a character. Rebuilt as a doll like the lobby preview.
    /// </summary>
    public SnapshotHumanoid? Humanoid;

    /// <summary>
    /// RandomSprite selection (greyscale sprite tinting like tools), applied to the rebuilt clone.
    /// </summary>
    public Dictionary<string, (string State, Color? Color)>? RandomSprite;
}

/// <summary>
/// Animation frame sent by the client for one sprite layer of one entity.
/// </summary>
/// <param name="Entity">Entity whose layer was captured.</param>
/// <param name="Layer">Sprite layer index.</param>
/// <param name="Frame">Animation frame index.</param>
[Serializable, NetSerializable]
public record struct SnapshotSpriteAnimation(NetEntity Entity, int Layer, int Frame);

/// <summary>
/// Captured animation frame for one sprite layer used when rebuilding the snapshot.
/// </summary>
/// <param name="Layer">Sprite layer index.</param>
/// <param name="Frame">Animation frame index.</param>
[Serializable, NetSerializable]
public record struct SnapshotSpriteLayerFrame(int Layer, int Frame);

/// <summary>
/// Icon-smoothing state sent by the client for one sprite layer of one entity.
/// </summary>
/// <param name="Entity">Entity whose layer was captured.</param>
/// <param name="Layer">Sprite layer index.</param>
/// <param name="State">RSI state name.</param>
[Serializable, NetSerializable]
public record struct SnapshotSpriteLayerStateCapture(NetEntity Entity, int Layer, string State);

/// <summary>
/// Icon-smoothing state applied when rebuilding the snapshot.
/// </summary>
/// <param name="Layer">Sprite layer index.</param>
/// <param name="State">RSI state name.</param>
[Serializable, NetSerializable]
public record struct SnapshotSpriteLayerState(int Layer, string State);

/// <summary>
/// Point-light state captured for one entity.
/// </summary>

[Serializable, NetSerializable]
public record struct SnapshotLight(
    bool Enabled,
    Color Color,
    float Radius,
    float Energy,
    float Softness,
    float Falloff,
    float CurveFactor,
    bool CastShadows,
    Vector2 Offset = default);

/// <summary>
/// Humanoid appearance needed to rebuild a character as a lobby-style doll.
/// Worn items are keyed by inventory slot name, held items by hand name.
/// </summary>
[Serializable, NetSerializable]
public sealed class SnapshotHumanoid
{
    public ProtoId<SpeciesPrototype> Species;

    public MarkingSet Markings = new();

    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = [];

    public HashSet<HumanoidVisualLayers> PermanentlyHidden = [];

    public Dictionary<HumanoidVisualLayers, SlotFlags> HiddenLayers = [];

    public HashSet<HumanoidVisualLayers> HideLayersOnEquip = [];

    public Dictionary<HumanoidVisualLayers, DisplacementData> MarkingsDisplacement = [];

    public Sex Sex;

    public Gender Gender;

    public Color SkinColor;

    public Color EyeColor;

    public bool EyeGlowing;

    public float Width = 1f;

    public float Height = 1f;

    public ProtoId<MarkingPrototype>? UndergarmentTop;

    public ProtoId<MarkingPrototype>? UndergarmentBottom;

    public Dictionary<string, SnapshotItem> Worn = [];

    public Dictionary<string, SnapshotItem> Held = [];

    public Dictionary<string, HandLocation> HeldHandLocations = [];

    /// <summary>
    /// Whether the character was standing. Down (crawling) characters are laid down on rebuild.
    /// </summary>
    public bool Standing = true;
}

/// <summary>
/// One item captured on a humanoid (worn or held), with its networked appearance and RandomSprite tint.
/// </summary>
[Serializable, NetSerializable]
public record struct SnapshotItem(EntProtoId Prototype, Dictionary<Enum, object>? Appearance, Dictionary<string, (string State, Color? Color)>? RandomSprite);
