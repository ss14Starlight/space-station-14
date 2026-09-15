using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ClawpackComponent : Component
{
    public readonly string ClawpackClawContainerId = "ClawpackClawContainer";
    
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId ItemPrototype = default!;
    
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId<ActionComponent> Action = default!;
    
    [DataField("requiredSlot"), AutoNetworkedField]
    public SlotFlags RequiredFlags = SlotFlags.BACK;

    [ViewVariables]
    public EntityUid? ItemUid = default!;
    
    [ViewVariables]
    public EntityUid? ActionEntity = default!;

    [ViewVariables]
    public ContainerSlot? ClawContainer = default!;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AttachedClawComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid AttachedUid = default!;
}