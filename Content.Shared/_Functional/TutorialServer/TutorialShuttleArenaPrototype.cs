using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Spawns a flyable cargo shuttle plus cargo-bay and ATS mini-stations for pilot/dock tutorials.
/// </summary>
[Prototype] //Tutorial: drop redundant type (RA0042)
public sealed partial class TutorialShuttleArenaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Optional full shuttle grid path. When unset, a compact trainer shuttle is built.
    /// </summary>
    [DataField]
    public ResPath? ShuttleMap;

    /// <summary>
    /// If docking the primary <see cref="ShuttleMap"/> fails, reload this map instead (tutorial dock airlock variant).
    /// </summary>
    [DataField]
    public ResPath? FallbackShuttleMap;

    /// <summary>
    /// Dock airlock proto for mini-stations (and the trainer shuttle when <see cref="ShuttleMap"/> is unset).
    /// </summary>
    [DataField]
    public EntProtoId DockProto = "AirlockGlassShuttle";

    /// <summary>
    /// Helm console on the trainer shuttle (ignored when loading <see cref="ShuttleMap"/>).
    /// </summary>
    [DataField]
    public EntProtoId ConsoleProto = "ComputerShuttle";

    /// <summary>
    /// Start docked to the cargo-bay mini-station.
    /// </summary>
    [DataField]
    public bool StartDocked = true;

    /// <summary>
    /// Map-space placement of the cargo-bay mini-station.
    /// </summary>
    [DataField]
    public Vector2 CargoBayOffset = new(28f, 0f);

    /// <summary>
    /// Map-space placement of the ATS mini-station (far enough for a short flight).
    /// </summary>
    [DataField]
    public Vector2 AtsOffset = new(95f, 0f);

    /// <summary>
    /// When false, the distant station is a dock-only platform (no ATS sell pallets / trade station).
    /// </summary>
    [DataField]
    public bool IncludeAtsSell = true;

    /// <summary>
    /// StationId for the home dock platform (DockShuttle / UndockShuttle markers).
    /// </summary>
    [DataField]
    public string HomeStationId = "cargo-bay";

    /// <summary>
    /// StationId for the distant dock platform.
    /// </summary>
    [DataField]
    public string DistantStationId = "ats";
}
