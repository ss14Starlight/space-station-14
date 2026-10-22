using Content.Shared.Actions;
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

[DataDefinition, Serializable, NetSerializable]
public sealed partial class EvolveChangelingEvent : EntityEventArgs;

public sealed partial class OpenChangelingColonyEvent : InstantActionEvent;
public sealed partial class RuptureChangelingEvent : InstantActionEvent;
public sealed partial class ArmChangelingNeurotoxinEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum ChangelingColonyUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed record ChangelingColonyEntry(NetEntity Entity, string Name, string? Occupant = null, bool CanEnter = true);

[Serializable, NetSerializable]
public sealed class ChangelingColonyUiState(
    List<ChangelingColonyEntry> parts,
    List<ChangelingColonyEntry> carriers,
    bool animateBlood,
    bool master = true,
    bool mindspace = false,
    bool canDetach = true,
    bool learned = true,
    bool evolved = false) : BoundUserInterfaceState
{
    public readonly List<ChangelingColonyEntry> Parts = parts;
    public readonly List<ChangelingColonyEntry> Carriers = carriers;
    public readonly bool AnimateBlood = animateBlood;
    public readonly bool Master = master;
    public readonly bool Mindspace = mindspace;
    public readonly bool CanDetach = canDetach;
    public readonly bool Learned = learned;
    public readonly bool Evolved = evolved;
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
public sealed class ChangelingDetachMessage(NetEntity part) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
}

[Serializable, NetSerializable]
public sealed class ChangelingSwitchMessage(NetEntity carrier) : BoundUserInterfaceMessage
{
    public readonly NetEntity Carrier = carrier;
}

[Serializable, NetSerializable]
public sealed class ChangelingAnimateBloodMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}
