namespace Content.Shared._Omu.Common.Construction
{
    [ByRefEvent]
    public record struct BigBuildAttemptEvent(EntityUid Machine, EntityUid? User, bool Cancelled = false);
}
