using Content.Shared.Slippery;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Slippery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlipOnHitComponent : Component
{
    /// <summary>
    /// Loads the data needed to determine how slippery something is.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlipperyEffectEntry SlipData = new();
}
