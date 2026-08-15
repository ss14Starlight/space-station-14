
using Robust.Shared.Prototypes;
using Content.Shared.Dataset;

namespace Content.Shared._Starlight.StoryGen;

[RegisterComponent]

public sealed partial class GrudgeBookComponent : Component
{
    public EntityUid? RightfulOwner;

    [DataField]
    public LocId preamble = "book-grudges-start";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> dataset = "GrudgeTemplate";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> relativeDataset = "GrudgeRelative";

    [DataField]
    public byte relativeDepth = 5; // maximum number of generations to go back

    [DataField]
    public byte multiGrudge = 5; // attempt to generate this number of grudges per grudging
}
