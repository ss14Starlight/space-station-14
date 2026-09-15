using Robust.Shared.GameStates;
using Content.Shared.Radio.EntitySystems;

namespace Content.Shared._Starlight.Clothing;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedHeadsetSystem)), AutoGenerateComponentState]
public sealed partial class HeadsetLoudModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// The font size to add to the radio message when loud mode is active. (Keep this low, as it can break the chatbox if it's too high.)
    /// </summary>

    [DataField, AutoNetworkedField]
    public int FontSize = 4;
}
