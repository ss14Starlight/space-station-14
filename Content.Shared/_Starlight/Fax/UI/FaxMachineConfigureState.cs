using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Fax.UI;

[NetSerializable, Serializable]
public sealed class FaxMachineConfigureState : BoundUserInterfaceState
{
    public string Name;
    public ProtoId<FaxGroupPrototype>? Grouping;
    public int Order;

    public FaxMachineConfigureState(string name, ProtoId<FaxGroupPrototype>? grouping, int order)
    {
        Name = name;
        Grouping = grouping;
        Order = order;
    }
}
