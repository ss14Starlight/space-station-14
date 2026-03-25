using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Silicons.IPC.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class IPCBrainHolderComponent : Component
{
    [DataField]
    public string BrainContainerSlotID = "borg_brain";

    [DataField]
    public SoundSpecifier BrainInsertionSound = new SoundPathSpecifier("/Audio/Items/pistol_magin.ogg");
    [DataField]
    public SoundSpecifier BrainExtractionSound = new SoundPathSpecifier("/Audio/Items/pistol_magout.ogg");

    [ViewVariables(VVAccess.ReadWrite)]
    public ContainerSlot BrainContainerSlot = default!;

    public EntityUid? BrainEntity => BrainContainerSlot.ContainedEntity;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class IPCBrainComponent : Component;