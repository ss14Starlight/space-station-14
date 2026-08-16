using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Starlight.DestinyDice;

[Prototype]
public sealed partial class DestinyDicePresetPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<DestinyDicePresetPrototype>))]
    public string[]? Parents { get; set; } = [];

    [AbstractDataField] public bool Abstract { get; set; }

    /// List of effect groups that will be used on component startup.
    public List<ProtoId<DestinyDiceEffectGroupPrototype>> EffectGroupIds { get; set; } = [];
}

[Prototype]
public sealed partial class DestinyDiceEffectGroupPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<DestinyDiceEffectGroupPrototype>))]
    public string[]? Parents { get; set; } = [];

    [AbstractDataField] public bool Abstract { get; set; }

    /// Populates effects for <see cref="Group"/> on map init.
    public List<ProtoId<DestinyDiceEffectPrototype>> EffectIds { get; set; } = [];

    ///
    public DestinyDiceEffectGroup Group { get; set; } = new();
}

[Prototype]
public sealed partial class DestinyDiceEffectPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<DestinyDiceEffectPrototype>))]
    public string[]? Parents { get; set; } = [];

    [AbstractDataField] public bool Abstract { get; set; }

    public DestinyDiceEffect Effect { get; set; }
}
