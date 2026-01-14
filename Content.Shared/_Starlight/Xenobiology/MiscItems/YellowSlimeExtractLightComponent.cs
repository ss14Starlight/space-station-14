using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology.MiscItems;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YellowSlimeExtractLightComponent : Component
{
    /// <summary>
    /// Whether the light is large or small.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsLarge = false;
}