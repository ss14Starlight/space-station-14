using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ClientCompControlComponent : Component
{
    [DataField, AutoNetworkedField] public HashSet<string> EnsuredComponents = [];
    [DataField, AutoNetworkedField] public HashSet<string> RemovedComponents = [];
    [DataField, AutoNetworkedField] public Dictionary<string, string> ViewVariablesWrites = [];
}