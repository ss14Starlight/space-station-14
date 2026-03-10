using Content.Server.StationEvents.Events;
using Content.Shared.Weather;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(WeatherRule))]
public sealed partial class WeatherRuleComponent : Component
{
    /// <summary>
    /// Weather type..
    /// </summary>
    [DataField]
    public ProtoId<WeatherPrototype> Weather;

    /// <summary>
    /// How long the weather should last. Null for forever.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    public MapId Map;
}