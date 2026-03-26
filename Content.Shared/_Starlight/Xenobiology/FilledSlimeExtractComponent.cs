using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology;

/// <summary>
/// Used to annotate pre-filled slime extract prototypes, so that they
/// don't trigger test failures in the "Spawn and delete count" test.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FilledSlimeExtractComponent : Component
{
}
