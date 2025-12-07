using Robust.Shared.GameStates;

namespace Content.Shared.Starlight.Traits.Components;

/// <summary>
/// Marker component for entities that breathe nitrogen.
/// Used for trait whitelisting/blacklisting.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NitrogenBreatherComponent : Component
{
}
