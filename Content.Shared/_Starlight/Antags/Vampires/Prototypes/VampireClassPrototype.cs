using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Antags.Vampires.Prototypes;

/// <summary>
/// Data-driven definition of a vampire subclass.
///
/// Adding a new vampire class should only require:
/// - a new class component
/// - a system for that component
/// - a <see cref="VampireClassPrototype"/> entry in YAML
/// </summary>
[Prototype("vampireClass")]
public sealed partial class VampireClassPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField(required: true)]
    public string Tooltip { get; private set; } = default!;
    [DataField(required: true)]
    public SpriteSpecifier Icon { get; private set; } = default!;
    [DataField(required: true)]
    public string ClassComponent { get; private set; } = default!;
    [DataField]
    public List<EntProtoId> Actions { get; private set; } = new();
}
