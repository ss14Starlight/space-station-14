using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Weather.Components;

/// <summary>
/// Starts weather when placed on a map.
/// </summary>
[RegisterComponent]
[EntityCategory("Spawner")]
public sealed partial class WeatherMarkerComponent : Component
{
    /// <summary>
    /// Weather prototype to apply to the map.
    /// example: WeatherRain
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Weather;

    /// <summary>
    /// Delay in seconds before weather starts after map init.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    /// <summary>
    /// Optional duration in seconds (null = infinite duration).
    /// </summary>
    [DataField]
    public TimeSpan? Duration;
}
