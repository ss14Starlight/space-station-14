using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Light;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlashImmunityTogglePointLightComponent : Component
{
    /// <summary>
    /// If true, the <see cref="SharedPointLightComponent"/> will be toggled OFF when the entity HAS flash immunity
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Invert = true;
}
