using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadSpawnCardsOnChosenComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId<RailroadCardComponent>> Cards = [];
}
