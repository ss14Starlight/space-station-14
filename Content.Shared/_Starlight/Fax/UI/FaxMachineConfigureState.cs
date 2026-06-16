using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Fax.UI;

[NetSerializable, Serializable]
public sealed class FaxMachineConfigureState(
    string name,
    ProtoId<FaxGroupPrototype>? currentGroup,
    ProtoId<FaxGroupPrototype>? intrinsicGroup,
    bool intrinsicLocked,
    int order,
    bool emagged)
    : BoundUserInterfaceState
{
    public string Name = name;
    public ProtoId<FaxGroupPrototype>? CurrentGroup = currentGroup;
    public ProtoId<FaxGroupPrototype>? IntrinsicGroup = intrinsicGroup;
    public bool IntrinsicLocked = intrinsicLocked;
    public int Order = order;
    public bool Emagged = emagged;
}
