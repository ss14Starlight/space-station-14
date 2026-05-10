using Robust.Shared.Containers;

namespace Content.Shared.VentCrawl.Tube.Components;

/// <summary>
/// A component representing a vent that you can crawl through
/// </summary>
[RegisterComponent]
public sealed partial class VentCrawlTubeComponent : Component
{
    public string ContainerId { get; set; } = "VentCrawlTube";

    public bool Connected;

    [ViewVariables]
    public Container Contents { get; set; } = null!;
}

[ByRefEvent]
public record struct GetVentCrawlsConnectableDirectionsEvent
{
    public Direction[] Connectable;
}
