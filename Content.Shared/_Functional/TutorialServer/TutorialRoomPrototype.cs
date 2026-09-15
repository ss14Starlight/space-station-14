using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Describes a multi-chamber practice suite built at tutorial start.
/// Chambers are laid out east-west; bolted doors between them unlock as goals advance.
/// Styles mirror Box/Bagel department floors, walls, and lighting.
/// </summary>
[Prototype] //Tutorial: drop redundant type (RA0042)
public sealed partial class TutorialRoomPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Interior width of each chamber in tiles (not counting walls).
    /// </summary>
    [DataField]
    public int ChamberWidth = 7;

    /// <summary>
    /// Interior height of each chamber in tiles (not counting walls).
    /// </summary>
    [DataField]
    public int ChamberHeight = 7;

    /// <summary>
    /// Fallback chamber count when the role does not supply a goal-driven count.
    /// </summary>
    [DataField]
    public int Chambers = 1;

    /// <summary>
    /// Hard cap on chambers (long curricula share the last room).
    /// </summary>
    [DataField]
    public int MaxChambers = 8;

    /// <summary>
    /// Legacy single-room width. Used only when <see cref="ChamberWidth"/> is left at default
    /// and this is explicitly set via YAML as <c>width</c>.
    /// </summary>
    [DataField("width")]
    public int? Width;

    /// <summary>
    /// Legacy single-room height.
    /// </summary>
    [DataField("height")]
    public int? Height;

    /// <summary>
    /// Floor tile prototype id (e.g. FloorKitchen, FloorWhite).
    /// </summary>
    [DataField(required: true)]
    public string FloorTile = "FloorSteel";

    /// <summary>
    /// Optional secondary floor used in a checker / aisle pattern.
    /// </summary>
    [DataField]
    public string? AltFloorTile;

    [DataField]
    public EntProtoId Wall = "WallSolid";

    /// <summary>
    /// Always-powered wall light (placed on chamber walls, not the floor center).
    /// </summary>
    [DataField]
    public EntProtoId Light = "AlwaysPoweredWallLight";

    /// <summary>
    /// Place wall lights this many tiles apart along the north wall of each chamber.
    /// Higher = more spread out / fewer fixtures.
    /// </summary>
    [DataField]
    public int LightSpacing = 5;

    /// <summary>
    /// Replace every other north-wall segment with a window.
    /// </summary>
    [DataField]
    public bool Windows = true;

    [DataField]
    public EntProtoId Window = "Window";

    /// <summary>
    /// Optional exterior door (crowbar practice, exits).
    /// </summary>
    [DataField]
    public TutorialRoomDoorSide? DoorSide;

    /// <summary>
    /// Which chamber gets the exterior door, counting from the end (1 = last, 2 = second-to-last).
    /// Use 2 when the final goal is a finish room with no door practice.
    /// </summary>
    [DataField]
    public int DoorChamberFromEnd = 1;

    [DataField]
    public EntProtoId Door = "AirlockMaint";

    /// <summary>
    /// Bolted inter-chamber gate door (unbolted + opened when the next goal starts).
    /// </summary>
    [DataField]
    public EntProtoId GateDoor = "Airlock";

    /// <summary>
    /// Unlock-goal index of the one gate that is crowbar practice instead of an automatic gate.
    /// That gate spawns as <see cref="PryGateDoor"/>: closed, unbolted and unpowered, and never
    /// auto-opened by <c>UnlockGatesForGoal</c>. Leave unset for suites with no pry drill.
    /// </summary>
    [DataField]
    public int? PryGateAtGoalIndex;

    /// <summary>
    /// Door prototype used for <see cref="PryGateAtGoalIndex"/>. Must be an intentionally
    /// unpowered airlock, or the power-forcing pass will make it open normally.
    /// </summary>
    [DataField]
    public EntProtoId PryGateDoor = "TutorialAirlockMaint";

    /// <summary>
    /// Static department furniture placed relative to chamber centers.
    /// </summary>
    [DataField]
    public List<TutorialRoomFurniture> Furniture = new();

    /// <summary>
    /// When true, skip the wall ring / atmos fill (open to space). Used only for EVA-style tutorials.
    /// </summary>
    [DataField]
    public bool ExposedToSpace;

    /// <summary>
    /// Fill the sealed room with breathable air (ignored when <see cref="ExposedToSpace"/>).
    /// </summary>
    [DataField]
    public bool FillAtmosphere = true;

    public int ResolveChamberWidth() => Width ?? ChamberWidth;
    public int ResolveChamberHeight() => Height ?? ChamberHeight;
}

[DataDefinition]
public sealed partial class TutorialRoomFurniture
{
    /// <summary>
    /// Chamber index (0 = first / spawn room). Clamped to the last chamber if out of range.
    /// </summary>
    [DataField]
    public int Room;

    [DataField(required: true)]
    public EntProtoId Id;

    /// <summary>
    /// Offset from that chamber's center.
    /// </summary>
    [DataField]
    public Vector2 Offset = Vector2.Zero;
}

public enum TutorialRoomDoorSide : byte
{
    North,
    South,
    East,
    West,
}
