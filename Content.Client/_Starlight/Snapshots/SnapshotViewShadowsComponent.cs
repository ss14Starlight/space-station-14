using System.Numerics;

namespace Content.Client._Starlight.Snapshots;

/// <summary>
/// tiles the photographer couldn't see, drawn black by <see cref="SnapshotViewShadowsOverlay"/>.
/// </summary>
[RegisterComponent]
public sealed partial class SnapshotViewShadowsComponent : Component
{
    [ViewVariables]
    public List<Vector2i> Tiles = [];

    /// <summary>
    /// Shadows cast by walls from the photographer's position, as a triangle list in grid-local coordinates.
    /// </summary>
    [ViewVariables]
    public Vector2[] Shadows = [];
}
