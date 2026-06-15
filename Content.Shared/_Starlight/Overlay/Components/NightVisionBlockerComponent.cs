using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Vision;

[RegisterComponent, NetworkedComponent]

// This component blocks NightVisionComponent. Used primarily for the 'Nightblind' trait.
public sealed partial class NightVisionBlockerComponent : Component
{

}
