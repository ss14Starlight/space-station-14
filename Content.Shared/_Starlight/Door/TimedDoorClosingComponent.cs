using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Door;

[RegisterComponent, NetworkedComponent]
public sealed partial class TimedDoorClosingComponent : Component
{
    /// <summary>
    /// Time until the door automatically closes after opening.
    /// </summary>
    [DataField]
    public TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(5);
}
