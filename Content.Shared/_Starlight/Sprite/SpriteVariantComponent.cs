using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Sprite;

/// <summary>
/// Marks an entity as having a server-picked sprite variant, applied by the
/// client to the entity's sprite layer on spawn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SpriteVariantComponent : Component
{
    /// <summary>
    /// RSI state names to pick from if <see cref="Variant"/> isn't set by spawn time.
    /// </summary>
    [DataField] public List<string> AvailableVariants = new();

    /// <summary>
    /// The chosen state name. Settable ahead of spawn (e.g. by a loadout) to
    /// skip the random pick.
    /// </summary>
    [DataField, AutoNetworkedField] public string? Variant;
}
