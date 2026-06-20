using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BaseLayerIdComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, ProtoId<HumanoidSpeciesSpriteLayer>?> Layers = new() { { "Default", null } };
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BaseLayerIdToggledComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, ProtoId<HumanoidSpeciesSpriteLayer>?> Layers = new() { { "Default", null } };
}
