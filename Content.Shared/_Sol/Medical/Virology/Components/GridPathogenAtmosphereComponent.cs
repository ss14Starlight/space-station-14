using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Per-grid tile store for non-gas airborne pathogen concentrations.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GridPathogenAtmosphereComponent : Component
{
    /// <summary>
    /// Active tiles keyed by grid indices. Values are pathogen ID → load.
    /// </summary>
    [DataField]
    public Dictionary<Vector2i, Dictionary<string, float>> Tiles = new();
}

[Serializable, NetSerializable]
public sealed class PathogenDebugOverlayDisableMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class PathogenDebugOverlayMessage : EntityEventArgs
{
    public NetEntity GridId { get; }
    public Vector2i BaseIdx { get; }
    public PathogenDebugOverlayTile?[] OverlayData { get; }

    public PathogenDebugOverlayMessage(NetEntity gridId, Vector2i baseIdx, PathogenDebugOverlayTile?[] overlayData)
    {
        GridId = gridId;
        BaseIdx = baseIdx;
        OverlayData = overlayData;
    }
}

[Serializable, NetSerializable]
public readonly record struct PathogenDebugOverlayTile(
    Vector2i Indices,
    float TotalLoad,
    (string PathogenId, float Load)[] Entries,
    AtmosDirection BlockedDirections);
