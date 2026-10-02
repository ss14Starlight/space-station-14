using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.Railroading.Components.Reward;

[RegisterComponent]
public sealed partial class RailroadDeliveryRewardComponent : Component
{
    [DataField("delivery")]
    public EntProtoId Delivery;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? Dataset = null;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? WrappedDataset = null;

    [NonSerialized]
    public EntityUid? RecipientMind;
}
