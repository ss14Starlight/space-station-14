using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

/// <summary>
///     Used for identifying other changelings.
///     Indicates that a changeling has bought the hivemind access ability.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HivemindComponent : Component
{
}
