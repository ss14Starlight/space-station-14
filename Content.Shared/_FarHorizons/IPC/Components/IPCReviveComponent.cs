using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._FarHorizons.Silicons.IPC.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IPCReviveComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public bool RebootButton = false;

    [DataField]
    [AutoNetworkedField]
    public TimeSpan RebootTime;

    [DataField]
    public SoundSpecifier? RebootSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_charge.ogg");

    [DataField]
    public SoundSpecifier? RebootFailSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_failed.ogg");

    [DataField]
    public SoundSpecifier? RebootSuccessSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_success.ogg");

    [DataField]
    public LocId CantReviveMessage = "ipc-revive-cant-revive";

    [DataField]
    public LocId RebootingMessage = "ipc-revive-reboot-started";

    [DataField]
    [AutoNetworkedField]
    public DamageSpecifier? DefibDamage = null;

    [DataField]
    [AutoNetworkedField]
    public bool DefibBatteryDrain = false;

    [DataField]
    public LocId RebootButtonLabel = "ipc-revive-button-label";

    [DataField]
    public LocId RebootButtonSubmenuLabel = "ipc-revive-button-submenu";

    [DataField]
    public string RebootButtonIcon = "/Textures/Interface/VerbIcons/zap.svg.192dpi.png";

    [DataField]
    public string RebootButtonSubmenuIcon = "/Textures/Interface/VerbIcons/group.svg.192dpi.png";

    [DataField]
    public SoundSpecifier? DamagedSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_success.ogg");

    [DataField]
    [AutoNetworkedField]
    public DamageThreshold DamagedThreshold = default!;

    public Entity<AudioComponent>? DamageSoundEnt = null;

    [DataDefinition, Serializable, NetSerializable]
    public sealed partial class DamageThreshold
    {
        [DataField]
        public int Min = 100;
        [DataField]
        public int? Max = null;
    }
}

[Serializable, NetSerializable]
public sealed partial class IPCRebootDoAfterEvent : SimpleDoAfterEvent;
