using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Mech.Components;

public sealed partial class MechComponent
{
    [DataField("nextUpdate", customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdateTime;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0.5);

    [DataField, AutoNetworkedField]
    public bool BreakOnEmag = true;

    [DataField("maintenance"), AutoNetworkedField]
    public bool MaintenanceMode;

    [DataField, AutoNetworkedField]
    public bool Internals;

    [DataField, AutoNetworkedField]
    public bool Light;

    [ViewVariables]
    public ContainerSlot GasTankSlot = default!;

    public const string GasTankSlotId = "mech-gas-tank-slot";

    [DataField]
    public EntityWhitelist? BatteryWhitelist;

    [ViewVariables]
    public bool PilotSupportWhitelistInitialized;

    [ViewVariables]
    public EntityWhitelist? PilotSupportBaseWhitelist;

    [DataField]
    public EntProtoId MechToggleLightAction = "ActionMechToggleLights";

    [DataField]
    public EntProtoId MechToggleInternalsAction = "ActionMechToggleInternals";

    [DataField]
    public EntityUid? MechToggleLightActionEntity;

    [DataField]
    public EntityUid? MechToggleInternalsActionEntity;

    [DataField]
    public SoundSpecifier ToggleLightSound = new SoundPathSpecifier("/Audio/Items/flashlight_pda.ogg");

    [DataField]
    public SoundSpecifier LowPowerSound = new SoundPathSpecifier("/Audio/Mecha/lowpower.ogg");

    [DataField]
    public SoundSpecifier NominalSound = new SoundPathSpecifier("/Audio/Mecha/nominal.ogg");

    [DataField]
    public SoundSpecifier NominalLongSound = new SoundPathSpecifier("/Audio/Mecha/longnanoactivation.ogg");

    [DataField]
    public SoundSpecifier PowerupSound = new SoundPathSpecifier("/Audio/Mecha/powerup.ogg");

    [DataField]
    public SoundSpecifier CriticalDamageSound = new SoundPathSpecifier("/Audio/Mecha/critnano.ogg");

    [DataField]
    public SoundSpecifier PowerDownSound = new SoundPathSpecifier("/Audio/Mecha/internaldmgalarm.ogg");

    [DataField]
    public SoundSpecifier? MaintenanceOnSound = new SoundPathSpecifier("/Audio/Machines/door_lock_on.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5),
    };

    [DataField]
    public SoundSpecifier? MaintenanceOffSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5),
    };

    [DataField]
    public bool FirstStart;

    [DataField]
    public bool PlayPowerSound = true;

    [DataField]
    public bool PlayPowerUpSound;
}
