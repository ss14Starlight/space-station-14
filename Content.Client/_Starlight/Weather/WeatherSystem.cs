// ReSharper disable CheckNamespace
namespace Content.Client.Weather;

public sealed partial class WeatherSystem
{
    /// <summary>
    /// Whether weather overlays are rendered on this client. Does not affect weather audio or gameplay.
    /// </summary>
    public bool DrawWeather { get; set; } = true;
}
