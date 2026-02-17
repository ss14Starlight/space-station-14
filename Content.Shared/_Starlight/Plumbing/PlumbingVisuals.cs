using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Plumbing;

[Serializable, NetSerializable]
public enum PlumbingVisuals : byte
{
    /// <summary>
    ///     Whether the plumbing machine is actively running.
    /// </summary>
    Running,

    /// <summary>
    ///     Directions that have PlumbingNodes defined.
    ///     Used to show jagged connectors in those directions.
    /// </summary>
    NodeDirections,

    /// <summary>
    ///     Directions that are connected.
    ///     Used to show smooth connector layer instead when connected.
    /// </summary>
    ConnectedDirections,

    /// <summary>
    ///     Directions that have inlet nodes.
    ///     Used to color inlet connectors red.
    /// </summary>
    InletDirections,

    /// <summary>
    ///     Directions that have outlet nodes.
    ///     Used to color outlet connectors blue.
    /// </summary>
    OutletDirections,

    /// <summary>
    ///     Whether the entity is covered by a floor tile.
    ///     Used to hide connector layers under floors without using SubFloorHide.
    /// </summary>
    CoveredByFloor,

    /// <summary>
    ///     Directions that have mixing inlet nodes.
    ///     Used to color mixing inlet connectors green.
    /// </summary>
    MixingInletDirections
}

[Serializable, NetSerializable]
public enum PlumbingVisualLayers : byte
{
    Base,
    Overlay
}
