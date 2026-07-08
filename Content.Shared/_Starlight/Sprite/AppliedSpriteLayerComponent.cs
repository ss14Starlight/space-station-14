using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Sprite;

/// <summary>
/// Simple system used to add a sprite onto an existing entity, for visual effects
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AppliedSpriteLayerComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier Sprite = default!;

    [DataField, AutoNetworkedField]
    public string Layer = "applied_sprite_layer";

    [DataField, AutoNetworkedField]
    public bool Glowing = false;
}
