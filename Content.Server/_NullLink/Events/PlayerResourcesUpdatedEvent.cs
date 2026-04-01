using Robust.Shared.Player;

namespace Content.Server._NullLink.Event;

[ByRefEvent]
public readonly struct PlayerResourcesUpdatedEvent
{
    public readonly ICommonSession Player;
    public readonly Dictionary<string, double> Resources;

    public PlayerResourcesUpdatedEvent(ICommonSession player, Dictionary<string, double> resources)
    {
        Player = player;
        Resources = resources;
    }
}