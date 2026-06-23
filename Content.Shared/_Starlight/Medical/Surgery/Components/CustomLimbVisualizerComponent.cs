using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Robust.Shared.GameStates;
namespace Content.Shared._Starlight.Medical.Surgery.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CustomLimbVisualizerComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, NetEntity?> Layers = [];

    [DataField]
    public HashSet<HumanoidVisualLayers> CachedLayers = [];

    [DataField]
    public Dictionary<HumanoidVisualLayers, DisplacementData> Displacements = [];
}
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CustomLimbComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Item;
}
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CustomLimbMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? VirtualPart;
}
