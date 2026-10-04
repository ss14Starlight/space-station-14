using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.MapRenderer;

public sealed class RenderedGridImage<T> where T : unmanaged, IPixel<T>
{
    public Image<T> Image;
    public Vector2 Offset { get; set; } = Vector2.Zero;
    public EntityUid? GridUid { get; set; }

    // Starlight-start
    /// <summary>
    /// Entity name of the grid, shown in the map viewer's grid list.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Grid-local position, in meters, of the image's bottom left corner.
    /// </summary>
    public Vector2 LocalOrigin { get; set; } = Vector2.Zero;
    // Starlight-end

    public RenderedGridImage(Image<T> image)
    {
        Image = image;
    }
}
