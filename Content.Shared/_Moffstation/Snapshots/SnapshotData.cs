using System.Numerics;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.MapText;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Snapshots;

/// <summary>
/// Viewers rebuild this on a client-only map, so clients never upload image data.
/// </summary>
/// <remarks>
/// Positions and rotations are relative to the camera-aligned snapshot frame, centered on the middle tile.
/// </remarks>
[Serializable, NetSerializable]
public sealed class SnapshotData
{

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

    public List<SnapshotTile> Tiles = [];

    public List<SnapshotEntity> Entities = [];

    /// <summary>
    /// Tiles in the frame the photographer couldn't see. They're drawn black,otherwise they would be the space backround
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
}

/// <param name="Roof">Roof color if the tile is roofed; roofs keep the map's ambient light off indoor tiles.</param>
[Serializable, NetSerializable]
public record struct SnapshotTile(
    Vector2i Offset,
    string TileId,
    byte Variant,
    byte RotationMirroring,
    Color? Roof);

[Serializable, NetSerializable]
public sealed class SnapshotEntity
{
    public EntProtoId Prototype;

    public Vector2 Offset;

    public Angle Rotation;

    public bool Anchored;

    public bool Ssd;

    /// <summary>
    /// Copy Apperance so the Rebuild view matches
    /// </summary>
    public Dictionary<Enum, object>? Appearance;

    public SnapshotLight? Light;

    /// <summary>
    /// Map text set on the entity itself (e.g. dev map signs), which respawning the prototype would lose.
    /// </summary>
    public MapTextComponentState? MapText;

    /// <summary>
    /// Index in <see cref="SnapshotData.Entities"/> to ensure draw order is respected
    /// for example if someone is buckled to something they get drawn ontop
    /// </summary>
    public int? BuckledTo;

    /// <summary>
    /// Making a Dummy similar to the Lobby screen instead of spawning a real prototype
    /// </summary>
    public SnapshotHumanoid? Humanoid;
}

[Serializable, NetSerializable]
public record struct SnapshotLight(
    bool Enabled,
    Color Color,
    float Radius,
    float Energy,
    float Softness,
    float Falloff,
    float CurveFactor);

/// <summary>
/// To ensure Character Items Inhand items ETC is carried over aswell as markings and such
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
}

[Serializable, NetSerializable]
public record struct SnapshotItem(EntProtoId Prototype, Dictionary<Enum, object>? Appearance);
