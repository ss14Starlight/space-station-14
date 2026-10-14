using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.ItemSummon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ItemSummonComponent : Component
{
    [DataField]
    public EntProtoId SummonableItem;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? SummonedItem;
}
