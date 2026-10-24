using Content.Shared._Starlight.Abstract.Conditions;
using Content.Shared._Starlight.Trail;
using Content.Shared._Starlight.Utility;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.GhostTheme;

[Prototype]
public sealed partial class GhostThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name")]
    private LocId _name = string.Empty;

    [DataField("description")]
    private LocId _description = string.Empty;

    public string Name => Loc.GetString(_name);

    public string Description => Loc.GetString(_description);

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

    [DataField]
    public ProtoId<GhostThemeCategoryPrototype> Category { get; private set; }
}
