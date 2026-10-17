// ReSharper disable CheckNamespace
using Content.Shared.Weather;

namespace Content.Client.Parallax;

public sealed partial class ParallaxOverlay
{
    private ParallaxLayerPrepared[] GetVisibleParallaxLayers(EntityUid uid, string parallax)
    {
        if (!_weather.DrawWeather && _entManager.HasComponent<WeatherStatusEffectComponent>(uid))
            return [];

        return _parallax.GetParallaxLayers(parallax);
    }
}
