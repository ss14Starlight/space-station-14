using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

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
    /// Sound played whenever the vehicle is started
    /// </summary>
    [DataField]
    public SoundSpecifier? StartUp;

    /// <summary>
    /// Sound played whenever the horn is press HONK
    /// </summary>
    [DataField]
    public SoundSpecifier? HornSound;

    [DataField, AutoNetworkedField]
    public string? BaseState;

    [DataField, AutoNetworkedField]
    public EntProtoId HornVehicleAction = "ActionVehicleHorn";
    
    [DataField, AutoNetworkedField] public EntityUid? HornVehicleActionEntity;

    [DataField, AutoNetworkedField]
    public EntProtoId TurnKeysAction = "ActionTurnKeys";
    
    [DataField, AutoNetworkedField] public EntityUid? TurnKeysActionEntity;
}