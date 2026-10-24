using Content.Shared.Actions;
using Content.Shared._Starlight.Medical.Body.Part;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Changeling;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ChangelingColonyMemberComponent : Component
{
    public EntityUid Mind;
    [DataField, AutoNetworkedField] public bool IsFragment;
    [DataField, AutoNetworkedField] public bool IsBlood;
    [DataField, AutoNetworkedField] public BodyPartType FragmentType;
    // Deliberately unassigned until the abilities for each body part are chosen.
    [DataField] public EntProtoId? EvolvedAction;
    public EntityUid? EvolvedActionEntity;
    public EntityUid? NeuroAction;
    public bool NeuroArmed;
    public TimeSpan NeuroReady;
    public EntityUid? CaptureTarget;
    public TimeSpan CaptureStarted;
    public bool EmpoweredCapture;
    public TimeSpan NextBlood;
    public bool SleepingWhileVacant;
    public bool ConfirmRupture;
    public uint RuptureRequest;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChangelingMindspaceComponent : Component
{
    public EntityUid Colony;
    public EntityUid? Listening;
    public TimeSpan OpenUiAt;
    [AutoNetworkedField] public EntityUid? Viewing;
    [AutoNetworkedField] public List<EntityUid> Bodies = new();
    public override bool SendOnlyToOwner => true;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class LearnChangelingColonyEvent : EntityEventArgs;

public sealed partial class OpenChangelingColonyEvent : InstantActionEvent;
public sealed partial class RuptureChangelingEvent : InstantActionEvent;
public sealed partial class ArmChangelingNeurotoxinEvent : InstantActionEvent;
public sealed partial class ToggleChangelingLivingBloodEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum ChangelingColonyUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed record ChangelingColonyEntry(NetEntity Entity, string Name, string? Occupant = null, bool CanEnter = true);

[Serializable, NetSerializable]
public sealed record ChangelingHiveMemberEntry(NetEntity Mind, string Name, bool CanSilence = true);

[Serializable, NetSerializable]
public sealed class ChangelingColonyUiState(
    List<ChangelingColonyEntry> parts,
    List<ChangelingColonyEntry> carriers,
    bool animateBlood,
    bool master = true,
    bool mindspace = false,
    bool canDetach = true,
    bool learned = true,
    bool evolved = false,
    bool confirmRupture = false,
    uint ruptureRequest = 0,
    List<ChangelingHiveMemberEntry>? members = null) : BoundUserInterfaceState
{
    public readonly List<ChangelingColonyEntry> Parts = parts;
    public readonly List<ChangelingColonyEntry> Carriers = carriers;
    public readonly bool AnimateBlood = animateBlood;
    public readonly bool Master = master;
    public readonly bool Mindspace = mindspace;
    public readonly bool CanDetach = canDetach;
    public readonly bool Learned = learned;
    public readonly bool Evolved = evolved;
    public readonly bool ConfirmRupture = confirmRupture;
    public readonly uint RuptureRequest = ruptureRequest;
    public readonly List<ChangelingHiveMemberEntry> Members = members ?? new();
}

[Serializable, NetSerializable]
public sealed class ChangelingObserveMessage(NetEntity carrier) : BoundUserInterfaceMessage
{
    public readonly NetEntity Carrier = carrier;
}

[Serializable, NetSerializable]
public sealed class ChangelingRecallMessage(NetEntity carrier) : BoundUserInterfaceMessage
{
    public readonly NetEntity Carrier = carrier;
}

[Serializable, NetSerializable]
public sealed class ChangelingLeaveBodyMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ChangelingSilenceMessage(NetEntity mind) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mind = mind;
}

[Serializable, NetSerializable]
public sealed class ChangelingRuptureMessage(uint request, bool confirmed) : BoundUserInterfaceMessage
{
    public readonly uint Request = request;
    public readonly bool Confirmed = confirmed;
}

[Serializable, NetSerializable]
public sealed class ChangelingDetachMessage(NetEntity part) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
}

[Serializable, NetSerializable]
public sealed class ChangelingSwitchMessage(NetEntity carrier) : BoundUserInterfaceMessage
{
    public readonly NetEntity Carrier = carrier;
}
