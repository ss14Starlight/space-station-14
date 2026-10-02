using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// ReSharper disable once CheckNamespace
namespace Content.MapRenderer;

public static class TileSlicer
{
    public static int Slice(Image<Rgba32> image, string directory, int tileSize, OutputFormat format)
    {
        var columns = (image.Width + tileSize - 1) / tileSize;
        var rows = (image.Height + tileSize - 1) / tileSize;

        IImageEncoder encoder = format switch
        {
            OutputFormat.webp => new WebpEncoder
            {
                Method = WebpEncodingMethod.BestQuality,
                FileFormat = WebpFileFormatType.Lossless,
                TransparentColorMode = WebpTransparentColorMode.Preserve,
            },
            _ => new PngEncoder(),
        };

        Parallel.For(0, columns * rows, i =>
        {
            var x = i % columns;
            var y = i / columns;

            var rect = new Rectangle(
                x * tileSize,
                y * tileSize,
                Math.Min(tileSize, image.Width - (x * tileSize)),
                Math.Min(tileSize, image.Height - (y * tileSize)));

            var tileDirectory = Path.Combine(directory, x.ToString(), y.ToString());
            Directory.CreateDirectory(tileDirectory);

            using var tile = image.Clone(ctx => ctx.Crop(rect));
            tile.Save(Path.Combine(tileDirectory, "0"), encoder);
        });

        return columns * rows;
    }
}
