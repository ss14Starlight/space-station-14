using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Fax.UI;

[NetSerializable, Serializable]
public sealed class FaxMachineConfigureState : BoundUserInterfaceState
{
    public string Name;
    public ProtoId<FaxGroupPrototype>? CurrentGroup;
    public ProtoId<FaxGroupPrototype>? IntrinsicGroup;
    public int Order;
    public bool Emagged;

    public FaxMachineConfigureState(string name, ProtoId<FaxGroupPrototype>? currentGroup, ProtoId<FaxGroupPrototype>? intrinsicGroup, int order, bool emagged)
    {
        Name = name;
        CurrentGroup = currentGroup;
        Order = order;
        Emagged = emagged;
    }
}
