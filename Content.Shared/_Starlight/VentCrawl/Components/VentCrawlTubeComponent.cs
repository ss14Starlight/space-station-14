using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.VentCrawl.Components;

/// <summary>
/// A component representing a vent that you can crawl through
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VentCrawlTubeComponent : Component
{
    [AutoNetworkedField]
    public List<EntityUid> ContainedHolders = new();

    public bool Connected;

    [DataField]
    public bool BlocksVision = true;
}

[ByRefEvent]
public record struct GetVentCrawlsConnectableDirectionsEvent
{
    public Direction[] Connectable;
}
