using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Authorable tutorial room section: prefer a station-cropped map template,
/// fall back to a procedural <see cref="TutorialRoomPrototype"/> when no crop exists.
/// </summary>
[Prototype] //Tutorial: drop redundant type (RA0042)
public sealed partial class TutorialRoomTemplatePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Sealed single-room grid cropped from a station (or salvage chunk).
    /// When null or load fails, <see cref="FallbackRoom"/> is used.
    /// </summary>
    [DataField]
    public ResPath? Map;

    /// <summary>
    /// Last-resort procedural single chamber used as the stamp template.
    /// </summary>
    [DataField]
    public ProtoId<TutorialRoomPrototype>? FallbackRoom;

    /// <summary>
    /// Human-readable note of which station/map the crop came from.
    /// </summary>
    [DataField]
    public string? SourceNote;

    [DataField]
    public EntProtoId GateDoor = "Airlock";

    /// <summary>
    /// Direction of the next stamped chamber relative to the current one.
    /// Science crops exit south into the hall; east often lands through the RD office.
    /// </summary>
    [DataField]
    public TutorialRoomDoorSide StampDirection = TutorialRoomDoorSide.East;

    [DataField]
    public bool FillAtmosphere = true;

    [DataField]
    public int MaxCopies = 8;

    /// <summary>
    /// Extra degrees added to perimeter wall-light facing (PointLight local offset faces -Y).
    /// Arrivals crop sprites need 180 so fixtures read as wall-mounted.
    /// </summary>
    [DataField]
    public float LightFacingOffsetDegrees;
}
