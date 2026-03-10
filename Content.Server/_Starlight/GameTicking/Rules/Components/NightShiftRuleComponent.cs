using Content.Server.StationEvents.Events;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(NightShiftRule))]
public sealed partial class NightShiftRuleComponent : Component
{
    [DataField] public LocId Announcement = new LocId("station-event-nightshift-alert");
}