using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using Content.Shared.Whitelist;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VehicleComponent : Component
{
    /// <summary>
    /// The person in control of this vehicle
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Rider;

    /// <summary>
    /// check if a vehicle requires ignition before allowing it to move
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequireIgnition = false;

    /// <summary>
    /// Check for keys in the vehicle
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool hasKeys = false;

    /// <summary>
    /// check if a vehicle requires ignition before allowing it to move
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Started = false;

    /// <summary>
    /// Is it powered by a power cell?
    /// </summary>
    [DataField]
    public bool CellPowered = true;

    /// <summary>
    /// check if a person is allow to wield a weapon for two handed bonuses
    /// </summary>
    [DataField("disallowWielding")]
    public bool DisallowWieldingGuns = false;

    /// <summary>
    /// check if a person takes stamina damage from shooting while in a vehicle
    /// </summary>
    [DataField("allowGunKnockback")]
    public bool AllowGunKnockback = false;

    /// <summary>
    /// just to check for if the vehicle is moving for other things
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool isMoving = false;

    /// <summary>
    /// just to check for if the vehicle is broken
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool isBroken = false;

    /// <summary>
    /// How many hands are blocked by the vehicle
    /// </summary>
    [DataField("handsNeeded")]
    public int HandsNeeded = 2;

    /// <summary>
    /// Vehicle health for integrity sake match it to the breakage trigger.
    /// </summary>
    [DataField("health")]
    public int Health = 150;

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
    /// Trigger crash?
    /// </summary>
    [DataField("allowCrashing"), AutoNetworkedField]
    public bool AllowCrashing = false;

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
    [DataField("soundHit", required: true)]
    public SoundSpecifier SoundHit = default!;

    [DataField]
    public EntityWhitelist? RiderWhitelist;

    [DataField]
    public string? BaseState;

    [DataField]
    public string? BrokenState;
}