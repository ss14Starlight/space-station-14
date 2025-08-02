using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Sprites;

/// <summary>
/// Component for automatically syncing sprite layers
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AnimationSyncComponent : Component
{
    /// <summary>
    /// Key of the layer to sync on. Must be mapped in the sprite component
    /// </summary>
    [DataField("layer", readOnly: true, required: true), AutoNetworkedField]
    public string LayerKey = string.Empty;

    /// <summary>
    /// Whether animations need to be paused when CCVars.ReducedMotion is defined
    /// </summary>
    [DataField("reduceMotion"), AutoNetworkedField]
    public bool ReduceMotion = false;
}
