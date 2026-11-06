// ReSharper disable CheckNamespace
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    private void DrawWeatherIfEnabled(
        in OverlayDrawArgs args,
        EntityUid mapUid,
        CachedResources res,
        Matrix3x2 invMatrix)
    {
        if (_weather.DrawWeather && _statusEffects.TryEffectsWithComp(mapUid, out _weatherSet))
            DrawWeather(args, res, _weatherSet, invMatrix);
    }
}
