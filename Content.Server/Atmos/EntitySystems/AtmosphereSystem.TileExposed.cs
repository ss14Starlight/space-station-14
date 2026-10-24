using System.Runtime.InteropServices;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private static readonly TimeSpan _tileExposedWindow = TimeSpan.FromSeconds(0.25);

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), float> _recentTileExposures = new();

    private TimeSpan _nextTileExposedReset;

    private bool ShouldRaiseTileExposed(EntityUid grid, Vector2i tile, float temperature)
    {
        var curTime = _gameTiming.CurTime;
        if (curTime >= _nextTileExposedReset)
        {
            _recentTileExposures.Clear();
            _nextTileExposedReset = curTime + _tileExposedWindow;
        }

        ref var maxTemperature = ref CollectionsMarshal.GetValueRefOrAddDefault(_recentTileExposures, (grid, tile), out var exists);
        if (exists && temperature <= maxTemperature)
            return false;

        maxTemperature = temperature;
        return true;
    }
}
