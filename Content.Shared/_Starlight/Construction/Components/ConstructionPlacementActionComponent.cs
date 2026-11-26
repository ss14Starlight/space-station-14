using Content.Shared.Construction.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Construction.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ConstructionPlacementActionComponent : Component
{
    [DataField(required: true)] public ProtoId<ConstructionPrototype> Construction;
}
