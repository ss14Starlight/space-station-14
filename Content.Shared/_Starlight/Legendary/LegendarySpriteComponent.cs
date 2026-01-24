using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Legendary;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LegendarySpriteComponent : Component
{
    [DataField, AutoNetworkedField]
    public ResPath RsiPath;
}
