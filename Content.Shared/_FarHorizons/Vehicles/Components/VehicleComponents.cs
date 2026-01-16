using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Content.Shared.Whitelist;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VehicleComponent : Component
{
    /// <summary>
    /// The person in control of this vehicle
    /// </summary>
    [DataField("rider"), AutoNetworkedField]
    public EntityUid? Rider;

    /// <summary>
    /// check if a vehicle requires ignition before allowing it to move
    /// </summary>
    [DataField("requireIgnition"), AutoNetworkedField]
    public bool requireIgnition = false;

    /// <summary>
    /// check if a vehicle requires ignition before allowing it to move
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Started = false;

    /// <summary>
    /// check if a person is allow to wield a weapon for two handed bonuses
    /// </summary>
    [DataField("disallowWielding"), AutoNetworkedField]
    public bool DisallowWieldingGuns = false;

    /// <summary>
    /// check if a person takes stamina damage from shooting while in a vehicle
    /// </summary>
    [DataField("allowGunKnockback"), AutoNetworkedField]
    public bool AllowGunKnockback = false;

    /// <summary>
    /// just to check for if the vehicle is moving for other things
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool isMoving = false;

    /// <summary>
    /// just to check for if the vehicle is moving for other things
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool isBroken = false;

    /// <summary>
    /// How many hands are blocked by the vehicle
    /// </summary>
    [DataField("handsNeeded")]
    public int HandsNeeded = 2;

    /// <summary>
    /// UID for the virtual item for the allowhands check
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? VirtualItem;

    /// <summary>
    /// how long does it take the vehicle to start
    /// </summary>
    [DataField("startupTime"), AutoNetworkedField]
    public TimeSpan startupTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// how long does it take the keys from a vehicle
    /// </summary>
    [DataField("timeToStealKeys"), AutoNetworkedField]
    public TimeSpan timeToStealKeys = TimeSpan.FromSeconds(3);

    /// <summary>
    /// the levels of friction the wearer is subected to, higher the number the more friction.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Friction = 2;

    /// <summary>
    /// Determines the turning ability of the wearer, Higher the number the less control of their turning ability.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FrictionNoInput = 6;

    /// <summary>
    /// Sets the speed in which the wearer accelerates to full speed, higher the number the quicker the acceleration.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Acceleration = 2;

    [DataField, AutoNetworkedField]
    public Direction? currentDirection;

    /// <summary>
    /// Trigger crash?
    /// </summary>
    [DataField("allowCrashing"), AutoNetworkedField]
    public bool AllowCrashing = false;

    /// <summary>
    /// Crashing speed if enabled
    /// </summary>
    [DataField("crashingSpeed"), AutoNetworkedField]
    public float CrashingSpeed = 6;

    /// <summary>
    /// Sound played whenever the vehicle is started
    /// </summary>
    [DataField]
    public SoundSpecifier? StartUp;

    /// <summary>
    /// Sound played whenever the horn is press HONK
    /// </summary>
    [DataField]
    public SoundSpecifier? HornSound;

    /// <summary>
    /// Sound played whenever running over someone or crashing
    /// </summary>
    [DataField("soundHit", required: true), ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier SoundHit = default!;

    [DataField]
    public EntityWhitelist? RiderWhitelist;

    [DataField, AutoNetworkedField]
    public string? BaseState;

    [DataField, AutoNetworkedField]
    public string? BrokenState;

    [DataField, AutoNetworkedField]
    public EntProtoId HornVehicleAction = "ActionVehicleHorn";
    
    [DataField, AutoNetworkedField] public EntityUid? HornVehicleActionEntity;

    [DataField, AutoNetworkedField]
    public EntProtoId TurnKeysAction = "ActionTurnKeys";
    
    [DataField, AutoNetworkedField] public EntityUid? TurnKeysActionEntity;

    [DataField, AutoNetworkedField]
    public EntProtoId ToggleTrunkAction = "ActionToggleTrunk";
    
    [DataField, AutoNetworkedField] public EntityUid? ToggleTrunkActionEntity;

    /// <summary>
    /// UID for the invisible headlight
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Headlight;
    
    [DataField, AutoNetworkedField]
    public EntProtoId ToggleSirenAction = "ActionVehicleToggleSecuritySiren";

    /// <summary>
    /// UID for the invisible sirenlight
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Sirenlight;

    /// <summary>
    /// Container that holds all the equipment for a vehicle
    /// </summary>
    [DataField, AutoNetworkedField]
    public string VehicleModsSlot = "vehicle_mods_container";
}