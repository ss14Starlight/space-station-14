using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Overlay.Components;

/// <summary>
/// This component blocks NightVisionComponent. Used primarily for the 'Nightblind' trait.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NightVisionBlockerComponent : Component
{

}
