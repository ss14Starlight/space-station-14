// ReSharper disable CheckNamespace
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Xenoarchaeology.Artifact.XAT.Components;

public sealed partial class XATCompNearbyComponent
{
    [DataField, AutoNetworkedField]
    public ProtoId<TagPrototype>? RequireTag;
}
