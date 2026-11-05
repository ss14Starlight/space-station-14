using Robust.Shared.GameStates;

namespace Content.Shared.Light.Components;

/// <summary>
/// Will draw shadows over tiles flagged as roof tiles on the attached grid. ImplicitRoofComponent will get removed if the grid has this component.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RoofComponent : Component
{
    public const int ChunkSize = 8;

    [DataField, AutoNetworkedField]
    public Color Color = Color.Black;

    /// <summary>
    /// Optional per-tile color overrides for roofed tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, Color> TileColors = new();

    /// <summary>
    /// Chunk origin and bitmask of value in chunk.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, ulong> Data = new();
}
