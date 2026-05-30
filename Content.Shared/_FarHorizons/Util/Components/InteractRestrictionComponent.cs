using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Util.Components;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Access(typeof(InteractRestrictionSystem))]
public sealed partial class InteractRestrictionComponent : Component
{
    [DataField, AutoNetworkedField]
    public InteractRestrictionList? RestrictInteractionSource;
    [DataField, AutoNetworkedField]
    public InteractRestrictionList? RestrictInteractionTarget;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class InteractRestrictionList {
    [DataField]
    public HashSet<ProtoId<TagPrototype>>? Blacklist;
    [DataField]
    public HashSet<ProtoId<TagPrototype>>? Whitelist;
}
