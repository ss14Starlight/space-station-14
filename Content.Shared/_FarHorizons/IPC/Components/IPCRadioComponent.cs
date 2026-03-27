using Content.Shared.Radio.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Silicons.IPC.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class IPCRadioComponent : Component
{
    [DataField]
    public bool CopyHeadsetKeys = false;
    [DataField("removeHeadset")]
    public bool RemoveHeadsetOnRoundstart = false;
    [DataField]
    public int KeysCapacity = 0;

    [DataField]
    public string EncryptionKeysContainerID = "key_slots";
    [DataField]
    public string HeadsetContainerID = "ears";
    [DataField]
    public SoundSpecifier KeyInsertionSound = new SoundPathSpecifier("/Audio/Items/pistol_magin.ogg");
    [DataField]
    public SoundSpecifier KeyExtractionSound = new SoundPathSpecifier("/Audio/Items/pistol_magout.ogg");

    [ViewVariables(VVAccess.ReadWrite)]
    public Container EncryptionKeysContainer = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public IntrinsicRadioTransmitterComponent RadioTransmitter = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public ActiveRadioComponent RadioReceiver = default!;
}