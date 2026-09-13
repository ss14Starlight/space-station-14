using System.Numerics;

namespace Content.Shared._Starlight.Arcade.Lancer;

/// <summary>
/// Odd-r (pointy-top) offset hex helpers for the Lancer arcade board.
/// </summary>
public static class LancerHex
{
    public const int GridSize = 12;

    /// <summary>Center-to-center horizontal spacing for pointy-top hexes of the given size.</summary>
    public const float DefaultHexSize = 32f;

    public readonly record struct Cube(int Q, int R, int S);

    public static Cube ToCube(LancerGridCoord pos)
    {
        // odd-r offset -> cube
        var q = pos.X - (pos.Y - (pos.Y & 1)) / 2;
        var r = pos.Y;
        return new Cube(q, r, -q - r);
    }

    public static LancerGridCoord FromCube(Cube cube)
    {
        // cube -> odd-r offset
        var x = cube.Q + (cube.R - (cube.R & 1)) / 2;
        return new LancerGridCoord(x, cube.R);
    }

    public static int Distance(LancerGridCoord a, LancerGridCoord b)
    {
        var ca = ToCube(a);
        var cb = ToCube(b);
        return (Math.Abs(ca.Q - cb.Q) + Math.Abs(ca.R - cb.R) + Math.Abs(ca.S - cb.S)) / 2;
    }

    // odd-r neighbor deltas for even / odd rows
    private static readonly int[] DxEven = [+1, 0, -1, -1, -1, 0];
    private static readonly int[] DyEven = [0, -1, -1, 0, +1, +1];
    private static readonly int[] DxOdd = [+1, +1, 0, -1, 0, +1];
    private static readonly int[] DyOdd = [0, -1, -1, 0, +1, +1];

    public static IEnumerable<LancerGridCoord> Neighbors(LancerGridCoord pos)
    {
        var even = (pos.Y & 1) == 0;
        for (var i = 0; i < 6; i++)
        {
            var dx = even ? DxEven[i] : DxOdd[i];
            var dy = even ? DyEven[i] : DyOdd[i];
            yield return new LancerGridCoord(pos.X + dx, pos.Y + dy);
        }
    }

    public static IEnumerable<LancerGridCoord> Line(LancerGridCoord from, LancerGridCoord to)
    {
        var n = Distance(from, to);
        if (n == 0)
        {
            yield return from;
            yield break;
        }

        var a = ToCube(from);
        var b = ToCube(to);
        for (var i = 0; i <= n; i++)
        {
            var t = i / (float) n;
            yield return FromCube(CubeRound(
                Lerp(a.Q, b.Q, t),
                Lerp(a.R, b.R, t),
                Lerp(a.S, b.S, t)));
        }
    }

    public static bool InBounds(LancerGridCoord pos) =>
        pos.X >= 0 && pos.X < GridSize && pos.Y >= 0 && pos.Y < GridSize;

    /// <summary>
    /// Pixel center of an odd-r pointy-top hex. Origin is the center of hex (0,0).
    /// </summary>
    public static Vector2 HexToPixel(LancerGridCoord pos, float size = DefaultHexSize)
    {
        // Pointy-top, flat-to-flat width = size. Vertical spacing = (√3/2)*size.
        var x = size * (pos.X + 0.5f * (pos.Y & 1));
        var y = size * (MathF.Sqrt(3f) / 2f) * pos.Y;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Convert a pixel (relative to the center of hex 0,0) into a grid coord.
    /// <paramref name="size"/> is the flat-to-flat width / horizontal center spacing.
    /// </summary>
    public static LancerGridCoord PixelToHex(Vector2 pixel, float size = DefaultHexSize)
    {
        // Pointy-top outer radius such that width = size (= sqrt(3) * R).
        var outer = size / MathF.Sqrt(3f);
        var aq = (MathF.Sqrt(3f) / 3f * pixel.X - 1f / 3f * pixel.Y) / outer;
        var ar = (2f / 3f * pixel.Y) / outer;
        return FromCube(CubeRound(aq, ar, -aq - ar));
    }

    /// <summary>Six corner offsets (pointy-top) relative to hex center, for drawing.</summary>
    public static Vector2[] GetHexCorners(Vector2 center, float size = DefaultHexSize)
    {
        var outer = size / MathF.Sqrt(3f);
        var corners = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 180f * (60f * i - 30f);
            corners[i] = center + new Vector2(outer * MathF.Cos(angle), outer * MathF.Sin(angle));
        }

        return corners;
    }

    private static float Lerp(int a, int b, float t) => a + (b - a) * t;

    private static Cube CubeRound(float q, float r, float s)
    {
        var rq = MathF.Round(q);
        var rr = MathF.Round(r);
        var rs = MathF.Round(s);

        var qDiff = MathF.Abs(rq - q);
        var rDiff = MathF.Abs(rr - r);
        var sDiff = MathF.Abs(rs - s);

        if (qDiff > rDiff && qDiff > sDiff)
            rq = -rr - rs;
        else if (rDiff > sDiff)
            rr = -rq - rs;
        else
            rs = -rq - rr;

        return new Cube((int) rq, (int) rr, (int) rs);
    }
}
