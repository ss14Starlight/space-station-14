using Robust.Shared.GameStates;

namespace Content.Shared.Starlight.Traits.Components;

/// <summary>
/// Marker component for entities that breathe oxygen.
/// Used for trait whitelisting/blacklisting.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OxygenBreatherComponent : Component
{
}
