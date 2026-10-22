// ReSharper disable CheckNamespace
namespace Content.Shared.Maps;

public sealed partial class ContentTileDefinition
{
    /// <summary>
    /// Causes the tile to appear as a wall on navigation maps.
    /// </summary>
    [DataField]
    public bool AppearsAsWallOnNavMap { get; private set; }
}
