using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace Content.Server._Starlight.Light;

/// <summary>
/// Reads the first channel of a light mask PNG.
/// </summary>
public static class LightMaskReader
{
    private static ReadOnlySpan<byte> s_signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool TryRead(Stream stream, out int width, out int height, out byte[] values)
    {
        width = height = 0;
        values = [];

        using var reader = new BinaryReader(stream);
        if (!reader.ReadBytes(8).AsSpan().SequenceEqual(s_signature))
            return false;

        var channels = 0;
        using var compressed = new MemoryStream();

        while (stream.Position < stream.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
            var type = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
            var data = reader.ReadBytes(length);
            reader.ReadBytes(4); // CRC

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                    var bitDepth = data[8];
                    var colorType = data[9];
                    var interlace = data[12];
                    channels = colorType switch
                    {
                        0 => 1, // grayscale
                        2 => 3, // RGB
                        4 => 2, // grayscale + alpha
                        6 => 4, // RGBA
                        _ => 0,
                    };

                    if (bitDepth != 8 || interlace != 0 || channels == 0)
                        return false;
                    break;
                case "IDAT":
                    compressed.Write(data);
                    break;
                case "IEND":
                    return channels != 0 && TryUnfilter(compressed, width, height, channels, out values);
            }
        }

        return false;
    }

    private static bool TryUnfilter(MemoryStream compressed, int width, int height, int channels, out byte[] values)
    {
        values = new byte[width * height];

        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);

        var stride = width * channels;
        var previous = new byte[stride];
        var current = new byte[stride];

        for (var y = 0; y < height; y++)
        {
            var filter = zlib.ReadByte();
            if (filter < 0)
                return false;

            zlib.ReadExactly(current);

            for (var i = 0; i < stride; i++)
            {
                var left = i >= channels ? current[i - channels] : 0;
                var up = previous[i];
                var upLeft = i >= channels ? previous[i - channels] : 0;

                current[i] = (byte) (current[i] + filter switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                });
            }

            for (var x = 0; x < width; x++)
            {
                values[(y * width) + x] = current[x * channels];
            }

            (previous, current) = (current, previous);
        }

        return true;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
