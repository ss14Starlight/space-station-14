using Content.Client.Players.PlayTimeTracking;
using Content.Shared._NullLink;

namespace Content.Client._NullLink;

public sealed class NullLinkPlayTimeManager : INullLinkPlayTimeManager
{
    [Dependency] private readonly JobRequirementsManager _jobRequirements = default!;

    public TimeSpan GetPlayTime(string server, Guid _, string tracker)
        => _jobRequirements.GetServerPlaytime(server, tracker);
}
