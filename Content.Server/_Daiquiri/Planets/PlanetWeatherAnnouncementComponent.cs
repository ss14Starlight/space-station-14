using Robust.Shared.Utility;

namespace Content.Server._Daiquiri.Planets;

[RegisterComponent]
public sealed partial class PlanetWeatherAnnouncementComponent : Component
{
    [DataField]
    public LocId? StartMessage;

    [DataField]
    public LocId? EndMessage;

    [DataField]
    public Color? Color;
}