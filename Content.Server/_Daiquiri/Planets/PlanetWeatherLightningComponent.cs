namespace Content.Server._Daiquiri.Planets;

/// <summary>
/// Makes a weather drop lightning and sets how often it does so. The bolt itself and the shock
/// damage come from the planet - see <see cref="PlanetWeatherSystem.TryStrike"/> - so a weather
/// without this component simply never strikes.
/// </summary>
[RegisterComponent]
public sealed partial class PlanetWeatherLightningComponent : Component
{
    /// <summary>
    /// Average number of strikes per second while this weather is running. Zero means no bolts.
    /// </summary>
    [DataField]
    public float ChancePerSecond;
}
