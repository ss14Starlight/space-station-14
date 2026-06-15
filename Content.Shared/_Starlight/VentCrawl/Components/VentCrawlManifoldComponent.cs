using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.VentCrawl.Components;

[RegisterComponent]
public sealed partial class VentCrawlManifoldComponent : Component
{
    /// <summary>
    /// Amount of layers
    /// </summary>
    [DataField]
    public int LayerCount = 5;
}
