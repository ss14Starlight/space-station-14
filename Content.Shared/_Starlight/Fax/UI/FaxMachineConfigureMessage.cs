using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Fax.UI;

[NetSerializable, Serializable]
public sealed class FaxMachineConfigureMessage(string name, ProtoId<FaxGroupPrototype>? grouping, int order)
    : BoundUserInterfaceMessage
{
    public string Name = name;
    public ProtoId<FaxGroupPrototype>? Grouping = grouping;
    public int Order = order;
}
