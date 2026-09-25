using Content.Shared.Silicons.Borgs;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Afterlight.Silicons.Borgs;

[Serializable, NetSerializable]
public sealed class BorgSelectSubtypeMessage(ProtoId<BorgTypePrototype> borgType, ProtoId<EntityPrototype>? subtype) : BoundUserInterfaceMessage // Starlight
{
    public ProtoId<BorgTypePrototype> BorgType = borgType; // Starlight
    public ProtoId<EntityPrototype>? Subtype = subtype;
}

[ByRefEvent]
public record struct AfterBorgTypeSelectEvent;
