using System.Collections.Generic;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight;

internal static class RuinTestHelpers
{
    public static List<(Vector2i Position, Tile Tile)> MakeSquareTiles(int sideLength, Tile tile)
    {
        var tiles = new List<(Vector2i Position, Tile Tile)>(sideLength * sideLength);
        for (var x = 0; x < sideLength; x++)
        {
            for (var y = 0; y < sideLength; y++)
            {
                tiles.Add((new Vector2i(x, y), tile));
            }
        }

        return tiles;
    }
}
