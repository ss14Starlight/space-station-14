using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ClawComponent : Component
{
    public readonly string ClawContainerId = "ClawContainer";

    [DataField(required: true), AutoNetworkedField]
    public EntityWhitelist AllowedEntities = default!;
    
    [DataField(required: true), AutoNetworkedField]
    public float ClawInteractionRange = default!;
    
    [ViewVariables]
    public ContainerSlot? Container = default!;
}