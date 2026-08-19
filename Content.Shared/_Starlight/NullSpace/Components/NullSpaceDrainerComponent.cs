using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.NullSpace.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class NullSpaceDrainerComponent : Component
{
    [DataField]
    public EntityUid? Target;

    /// <summary>
    /// Points drained by energy/sec
    /// </summary>
    [DataField]
    public int Points = 100;
}
