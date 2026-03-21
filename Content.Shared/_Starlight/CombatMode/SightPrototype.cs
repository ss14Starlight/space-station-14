using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.CombatMode;

[Prototype("sight")]
public sealed partial class SightPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;

    [DataField]
    public SightType SightType = SightType.Melee;

    [DataField]
    public string? BoltVariant = null;

    [DataField]
    public bool Bolt = false;

    [DataField(required: true)]
    public SpriteSpecifier Sprite = SpriteSpecifier.Invalid;

    [DataField]
    public bool ShowCursor = true;

    [DataField]
    public Color MainColor { private set; get; } = Color.White.WithAlpha(0.3f);

    [DataField]
    public Color StrokeColor { private set; get; } = Color.Black.WithAlpha(0.5f);

    [DataField]
    public float Scale = 0.6f;
}

public enum SightType : int
{
    Ranged,
    Melee,
    Universal,
}