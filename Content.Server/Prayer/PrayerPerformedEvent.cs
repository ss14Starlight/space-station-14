//Begin Starlight (DevoutChap)
namespace Content.Server.Prayer;

[ByRefEvent]
public record struct PrayerPerformedEvent(EntityUid User);

//End Starlight