using Content.Shared._Starlight.Abstract.Conditions;
using Content.Shared._Starlight.Trail;
using Content.Shared.Starlight.Utility;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.GhostTheme;

[Prototype("ghostTheme")]
public sealed partial class GhostThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    [DataField("description")]
    public string Description { get; private set; } = string.Empty;

    [DataField("spriteSpecifier", required: true)]
    public ExtendedSpriteSpecifier SpriteSpecifier { get; private set; } = default!;

    [DataField("colorizeable")]
    public bool Colorizeable = false;

    [DataField("private")]
    public bool Private = false;

    [DataField("trail")]
    public TrailSettings? Trail = null;

    [DataField("requirements")]
    public List<BaseRequirement> Requirements = [];
}
