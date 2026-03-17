using Robust.Shared.GameStates;

namespace Content.Shared._ST.CosmicCult.Components;

/// <summary>
/// Component for handling The Monument's collision.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MonumentCollisionComponent : Component
{
    // Starlight Edit: Changed this to an empty component with Comp Checks instead of a ``HasCollision`` bool
}
