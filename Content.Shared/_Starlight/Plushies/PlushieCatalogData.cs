using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Plushies;

[DataDefinition]
public sealed partial class PlushieLimits
{
    [DataField]
    public int Slots { get; private set; } = 4;

    [DataField]
    public int MaxOffset { get; private set; } = 10;

    [DataField]
    public int Phrases { get; private set; } = 4;

    [DataField]
    public int PhraseLength { get; private set; } = 60;

    [DataField]
    public int SpriteSize { get; private set; } = 32;
}

[DataDefinition]
public sealed partial class PlushieTintGroup
{
    [DataField("id", required: true)]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? Name { get; private set; }

    [DataField(required: true)]
    public List<Color> Palette { get; private set; } = [];
}

[DataDefinition]
public sealed partial class PlushieSound
{
    [DataField("id", required: true)]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? Name { get; private set; }

    [DataField]
    public string? Note { get; private set; }

    [DataField(required: true)]
    public SoundSpecifier Sound { get; private set; } = default!;
}
