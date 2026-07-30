using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Plushies;

[Prototype]
public sealed partial class PlushieCatalogPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public PlushieLimits Limits { get; private set; } = default!;

    [DataField(required: true)]
    public List<PlushieLayer> Layers { get; private set; } = [];

    [DataField(required: true)]
    public List<PlushieTintGroup> Tints { get; private set; } = [];

    [DataField(required: true)]
    public List<PlushieSound> Sounds { get; private set; } = [];

    public PlushieLayer? Layer(string id) => Layers.FirstOrDefault(l => l.ID == id);

    public PlushieTintGroup? Tint(string id) => Tints.FirstOrDefault(t => t.ID == id);

    public PlushieSound? Sound(string id) => Sounds.FirstOrDefault(s => s.ID == id);
}

[DataDefinition]
public sealed partial class PlushieLayer
{
    [DataField("id", required: true)]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? Name { get; private set; }

    [DataField(required: true)]
    public ResPath Rsi { get; private set; }

    [DataField]
    public string? Tint { get; private set; }

    [DataField]
    public bool Required { get; private set; }

    [DataField]
    public bool Offsettable { get; private set; } = true;

    [DataField(required: true)]
    public List<PlushieSprite> Sprites { get; private set; } = [];

    public PlushieSprite? Sprite(string id) => Sprites.FirstOrDefault(s => s.ID == id);

    public SpriteSpecifier.Rsi? Specifier(string id)
        => Sprite(id) is { } sprite ? new SpriteSpecifier.Rsi(sprite.Rsi ?? Rsi, sprite.State ?? sprite.ID) : null;
}

[DataDefinition]
public sealed partial class PlushieSprite
{
    [DataField("id", required: true)]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? Name { get; private set; }

    [DataField]
    public string? State { get; private set; }

    [DataField]
    public ResPath? Rsi { get; private set; }

    [DataField]
    public bool Untinted { get; private set; }

    [DataField]
    public string? Species { get; private set; }
}
