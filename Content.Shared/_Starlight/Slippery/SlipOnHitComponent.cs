using Content.Shared.Slippery;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Slippery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlipOnHitComponent : Component
{
    /// <summary>
    /// How much stamina damage should this component do on slip?
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StaminaDamage = 25f;

    /// <summary>
    /// Loads the data needed to determine how slippery something is.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlipperyEffectEntry SlipData = new();

    /// <summary>
    /// Whether a worn hardsuit prevents this hit from applying slip effects.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BlockedByHardsuit;
}
