using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Legendary;

/// <summary>
///     Defines a pool of localization keys used to generate a short story?
/// </summary>
[Prototype("legendaryStory")]
public sealed partial class StoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<LocId> Opens = new();

    [DataField]
    public List<LocId> Mids = new();

    [DataField]
    public List<LocId> Ends = new();
}
