using Robust.Shared.GameStates;

namespace Content.Shared.Humanoid;

[RegisterComponent, NetworkedComponent]
public sealed partial class SmallSpeciesComponent : Component
{
    [DataField]
    public int HandsNeeded = 2;
}
