using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.EdgeConnection;

/// <summary>
/// Enables visual edge connections between entities when placed adjacent to each other.
/// Entities with matching connection keys will form connections.
/// Works with GenericVisualizer to update sprites based on neighbor state.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EdgeConnectionComponent : Component
{
    /// <summary>
    /// Key used to identify which entities can connect to each other.
    /// Only entities with matching keys will form connections.
    /// </summary>
    [DataField]
    public string ConnectionKey = "default";

    /// <summary>
    /// Which directions are allowed to form connections.
    /// Must be set explicitly - defaults to None (no connections).
    /// Entities will only connect if they have matching rotations.
    /// </summary>
    [DataField]
    public EdgeConnectionFlags AllowedDirections = EdgeConnectionFlags.None;
}
