using Robust.Shared.GameStates;
using Content.Shared.Radio.EntitySystems;

namespace Content.Shared._Starlight.Clothing;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedHeadsetSystem)), AutoGenerateComponentState]
public sealed partial class HeadsetLoudModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;
}
