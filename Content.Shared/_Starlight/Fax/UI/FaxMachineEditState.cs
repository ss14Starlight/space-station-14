using Content.Shared._Starlight.Fax;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Fax.UI;

[NetSerializable, Serializable]
public sealed class FaxMachineEditState : BoundUserInterfaceState
{
    public string Name;
    public List<FaxGroupingPrototype> Groupings;

    public FaxMachineEditState(string name, List<FaxGroupingPrototype> groupings)
    {
        Name = name;
        Groupings = groupings;
    }
}