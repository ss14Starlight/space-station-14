using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Spawns a cargo-bay box station plus a nearby space spawn for Space Dragon practice.
/// </summary>
[Prototype] //Tutorial: drop redundant type (RA0042)
public sealed partial class TutorialDragonArenaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Map-space placement of the prey station (cargo-bay box).
    /// </summary>
    [DataField]
    public Vector2 StationOffset = new(40f, 0f);

    /// <summary>
    /// Map-space dragon spawn relative to <see cref="StationOffset"/>.
    /// </summary>
    [DataField]
    public Vector2 SpaceSpawnOffset = new(-25f, 5f);

    /// <summary>
    /// Offset from the space spawn for the ground pinpointer.
    /// </summary>
    [DataField]
    public Vector2 PinpointerOffset = new(1.5f, 0f);

    /// <summary>
    /// Dock airlock proto for the box station (matches cargo bay).
    /// </summary>
    [DataField]
    public EntProtoId DockProto = "AirlockGlassShuttle";

    /// <summary>
    /// <see cref="TutorialDockStationComponent.StationId"/> for the prey bay.
    /// </summary>
    [DataField]
    public string StationId = "dragon-prey";
}
