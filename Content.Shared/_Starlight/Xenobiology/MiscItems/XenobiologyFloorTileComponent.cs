using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.MiscItems;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenobiologyFloorTileComponent : Component
{
    [DataField("entity", required: true), AutoNetworkedField]
    public EntProtoId Entity;
}