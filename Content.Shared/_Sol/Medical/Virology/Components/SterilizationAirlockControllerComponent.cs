using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Powered floor controller that cycles a paired sterilization chamber between two linked airlocks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SterilizationAirlockControllerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SterilizationStrength = 1f;

    [DataField, AutoNetworkedField]
    public TimeSpan FogDuration = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan FadeDuration = TimeSpan.FromSeconds(0.75);

    [DataField, AutoNetworkedField]
    public TimeSpan ClosingTimeout = TimeSpan.FromSeconds(8);

    [DataField, AutoNetworkedField]
    public bool RequiresPower = true;

    [DataField]
    public EntProtoId FogPrototype = "SolSterilizationFog";

    [DataField, AutoNetworkedField]
    public SterilizationControllerPhase Phase = SterilizationControllerPhase.Idle;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseEndsAt;

    /// <summary>
    /// Door that was last opened as the entrance before the cycle began.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? EntranceDoor;

    [DataField, AutoNetworkedField]
    public EntityUid? ExitDoor;

    [DataField, AutoNetworkedField]
    public EntityUid? DoorA;

    [DataField, AutoNetworkedField]
    public EntityUid? DoorB;

    /// <summary>
    /// Which linked chamber door faces the public side and is bolted by the quarantine input.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SterilizationControllerDoor QuarantineDoor = SterilizationControllerDoor.A;

    [DataField, AutoNetworkedField]
    public bool QuarantineLocked;

    /// <summary>
    /// After an inbound cycle opens the inner door, the next inner close is a follow-up
    /// cleanse that must not reopen the outer door.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AwaitingInnerResterilize;

    /// <summary>
    /// Whether the current cycle should open the opposite door after sterilization.
    /// False for follow-up cleanses after inbound entry through the outer door.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OpenExitAfterSterilization = true;

    [ViewVariables]
    public List<EntityUid> ActiveFog = new();
}

[Serializable, NetSerializable]
public enum SterilizationControllerDoor : byte
{
    A,
    B,
}

[Serializable, NetSerializable]
public enum SterilizationControllerPhase : byte
{
    Idle,
    Closing,
    Fogging,
    Fading,
    OpeningExit,
    Fault,
}

[Serializable, NetSerializable]
public enum SterilizationControllerVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum SterilizationControllerVisualState : byte
{
    Off,
    Closing,
    Fogging,
    Fault,
}

/// <summary>
/// Temporary interlock preventing linked doors from opening during an active sterilization cycle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SterilizationDoorLockComponent : Component;

/// <summary>
/// Visual-only sterilization fog that fades out before the exit door opens.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SterilizationFogComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan FadeStartsAt;

    [DataField, AutoNetworkedField]
    public TimeSpan FadeEndsAt;
}
