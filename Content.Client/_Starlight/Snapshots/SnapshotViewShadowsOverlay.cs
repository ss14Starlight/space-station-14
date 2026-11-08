using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Starlight.Snapshots;

/// <summary>
/// Hidden tiles and shadows for a snapshot grid, drawn black by <see cref="SnapshotViewShadowsOverlay"/>.
/// Kept here with the overlay that renders it; only snapshot grids have this component.
/// </summary>
[RegisterComponent]
public sealed partial class SnapshotViewShadowsComponent : Component
{
    /// <summary>
    /// Tiles the photographer couldn't see, as frame offsets.
    /// </summary>
    [ViewVariables]
    public List<Vector2i> Tiles = [];

    /// <summary>
    /// Shadows cast by walls from the photographer's position, as a triangle list in grid-local coordinates.
    /// </summary>
    [ViewVariables]
    public Vector2[] Shadows = [];
}

/// <summary>
/// Draws what a snapshot's photographer couldn't see black, like out-of-sight areas in game: wall shadows cast from
/// where they stood, plus tiles the snapshot has no data for.
/// Only snapshot grids have <see cref="SnapshotViewShadowsComponent"/>, so this draws nothing in the normal view.
/// </summary>
public sealed partial class SnapshotViewShadowsOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public SnapshotViewShadowsOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entityManager.AllEntityQueryEnumerator<SnapshotViewShadowsComponent, TransformComponent>();
        while (query.MoveNext(out _, out var shadows, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            handle.SetTransform(_transform.GetWorldMatrix(xform));
            if (shadows.Shadows.Length > 0)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, shadows.Shadows, Color.Black);

            foreach (var tile in shadows.Tiles)
            {
                handle.DrawRect(Box2.FromDimensions(tile, Vector2.One), Color.Black);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
