using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Pollen.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PollenCollectorComponent : Component
{
    [DataField]
    public List<string> CollectedPollen = new();
}
