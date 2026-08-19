
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

    /// <summary>
    /// When generating grudges, how many generations back can they go?
    /// </summary>
    [DataField]
    public byte relativeDepth = 1;

    /// <summary>
    /// If someone deserves a grudge, how many complaints do we have against them?
    /// </summary>
    [DataField]
    public byte multiGrudge = 5;
}
