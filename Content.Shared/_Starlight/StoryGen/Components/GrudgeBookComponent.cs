
using Robust.Shared.Prototypes;
using Content.Shared.Dataset;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.StoryGen;

[Access(typeof(SharedGrudgeSystem))]
public sealed partial class GrudgeBookComponent : Component
{
    public EntityUid? RightfulOwner;
    public RobustRandom rng;

    [DataField]
    public LocId preamble = "book-grudges-start";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> dataset = "GrudgeTemplate";
}
