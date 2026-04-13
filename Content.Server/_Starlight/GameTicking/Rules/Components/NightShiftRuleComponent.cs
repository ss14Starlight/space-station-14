using Content.Server.StationEvents.Events;
using Content.Shared.Light.Components;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(NightShiftRule))]
public sealed partial class NightShiftRuleComponent : Component
{
    /// <summary>
    /// The announcement when the effect is disabled due to a too-high alert leve.
    /// </summary>
    [DataField] public LocId DisableAnnouncement;

    /// <summary>
    /// The announcement when the effect is enabled due to a no-longer too-high alert level.
    /// </summary>
    [DataField] public LocId EnableAnnouncement;

    /// <summary>
    /// Which alert levels are permissible for this event. This is checked against when initiating the event, and on
    /// alert level change, to be sure the event can keep going without being unnecessarily obtrusive.
    /// </summary>
    [DataField] public List<string> PermittedAlertLevels;

    /// <summary>
    /// The light energy modifier while the night shift is active.
    /// </summary>
    [DataField] public float LightEnergyMultiplier;
}
