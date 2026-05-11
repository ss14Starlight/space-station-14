using System.Linq;
using System.Numerics;
using Content.Client.Pinpointer.UI;
using Content.Shared._Starlight.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Shuttles;

/// <summary>
/// A static (non-interactive) nav-map used by the drop pod console.
/// Draws diagonal sector dividers and highlights the selected N/E/S/W quadrant.
/// </summary>
public sealed class DropPodNavMapControl : NavMapControl
{
    /// <summary>Which sector is currently highlighted.</summary>
    public DropPodDirection? SelectedDirection;

    /// <summary>Centers the map view on the given world-space position on the station grid.</summary>
    public void CenterOnWorldPos(EntityUid gridUid, Vector2 worldCenter)
    {
        // CenterToCoordinates expects EntityCoordinates relative to the grid
        var xformSys = EntManager.System<SharedTransformSystem>();
        var localPos = Vector2.Transform(worldCenter, xformSys.GetInvWorldMatrix(gridUid));
        CenterToCoordinates(new EntityCoordinates(gridUid, localPos));
    }

    // Disable all dragging and mouse interaction
    protected override bool Draggable => false;

    public DropPodNavMapControl() : base()
    {
        RectClipContent = true;

        // Hide the top toolbar (zoom / recenter / beacons) added by NavMapControl.
        if (ChildCount > 0)
        {
            var topContainer = Children.First();
            if (topContainer.ChildCount > 0)
                topContainer.Children.First().Visible = false;
        }

        // Map is view-only — do not wire MapClickedAction
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var cx = PixelSize.X / 2f;
        var cy = PixelSize.Y / 2f;
        var center = new Vector2(cx, cy);
        var tl = Vector2.Zero;
        var tr = new Vector2(PixelSize.X, 0f);
        var bl = new Vector2(0f, PixelSize.Y);
        var br = new Vector2(PixelSize.X, PixelSize.Y);

        // Highlight selected sector (corner-triangle quadrant, Y flipped: top = North)
        if (SelectedDirection.HasValue)
        {
            var overlay = Color.OrangeRed.WithAlpha(0.22f);
            Vector2[] verts = SelectedDirection.Value switch
            {
                DropPodDirection.North => [center, tl, tr],
                DropPodDirection.South => [center, br, bl],
                DropPodDirection.East  => [center, tr, br],
                DropPodDirection.West  => [center, bl, tl],
                _                      => [],
            };
            if (verts.Length > 0)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, overlay);
        }

        // Draw diagonal divider lines from center to each corner
        var lineColor = Color.White.WithAlpha(0.35f);
        handle.DrawLine(center, tl, lineColor);
        handle.DrawLine(center, tr, lineColor);
        handle.DrawLine(center, bl, lineColor);
        handle.DrawLine(center, br, lineColor);
    }
}
