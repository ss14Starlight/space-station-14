using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
public sealed partial class DamnedComponent : Component
{
    public List<ProtoId<DamnationPrototype>> Damnations = new();

    public int NetCost = 0;
}