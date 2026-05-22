using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OnSolutionChangedTraitsComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<ProtoId<OnSolutionChangedTraitPrototype>, FixedPoint2> Traits = new();

    /// <summary>
    /// The name of the container that holds the solution.
    /// Needed so that the solution changing can communicate with the container itself.
    /// </summary>
    [ViewVariables, AutoNetworkedField, DataField("containerName", required: true)]
    public string ContainerName = string.Empty;
}
