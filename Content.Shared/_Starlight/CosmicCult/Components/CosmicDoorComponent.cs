using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CosmicDoorComponent : Component
{
    /// <summary>
    /// Time before the cosmic door automatically closes after opening.
    /// </summary>
    [DataField]
    public TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(5);
}
