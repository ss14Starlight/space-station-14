using Content.Server._Starlight.StationEvents.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.StationEvents.Components;

[RegisterComponent, Access(typeof(WeatherRule))]
public sealed partial class WeatherRuleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Weather;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0);

    public MapId Map;
}
