using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Knapping;

[Prototype]
public sealed partial class KnappingRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public string Description = "knapping-recipe-description-generic";

    [DataField]
    public string Category = "knapping-category-misc";

    [DataField]
    public int Difficulty = 1;

    [DataField(required: true)]
    public string Output = string.Empty;

    [DataField]
    public bool AllowOffset = true;

    [DataField(required: true)]
    public List<string> Pattern = [];
}
