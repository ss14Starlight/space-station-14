namespace Content.Client._Starlight.Sprites;

/// <summary>
/// Component for automatically syncing sprite layers
/// </summary>
[RegisterComponent]
public sealed partial class AnimationSyncComponent : Component
{
    /// <summary>
    /// Key of the layer to sync on. Must be mapped in the sprite component
    /// </summary>
    [DataField("layer", required: true)]
    public string layer = string.Empty;

    /// <summary>
    /// Whether animations need to be paused when CCVars.ReducedMotion is defined
    /// </summary>
    [DataField("reduceMotion")]
    public bool reduceMotion = false;
}
