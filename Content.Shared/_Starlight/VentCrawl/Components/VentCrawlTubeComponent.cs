using Robust.Shared.GameStates;

namespace Content.Shared.VentCrawl.Tube.Components;

/// <summary>
/// A component representing a vent that you can crawl through
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VentCrawlTubeComponent : Component
{
    public string ContainerId { get; set; } = "VentCrawlTube";

    public bool Connected;
}

[ByRefEvent]
public record struct GetVentCrawlsConnectableDirectionsEvent
{
    public Direction[] Connectable;
}
