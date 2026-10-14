namespace Content.Server._Starlight.Bible;

[ByRefEvent]
public record struct BibleThwackEvent(EntityUid User, bool Handled = false);
