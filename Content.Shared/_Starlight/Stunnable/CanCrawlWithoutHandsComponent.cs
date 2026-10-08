using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Stunnable;

/// <summary>
/// Allows an entity to crawl even without hands.
/// Add this component to any entity that should be able to crawl with zero hands,
/// such as cyborgs when no module is selected.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CanCrawlWithoutHandsComponent : Component
{
}
