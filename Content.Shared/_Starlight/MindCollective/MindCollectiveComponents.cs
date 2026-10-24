using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.MindCollective;

/// <summary>Creates a collective for the first mind to inhabit this entity.</summary>
[RegisterComponent]
public sealed partial class MindCollectiveCreatorComponent : Component
{
    [DataField] public EntProtoId ObserverPrototype = "MindCollectiveObserver";
    [DataField] public EntProtoId? VacancyPrototype = "MindCollectiveVacancy";
    [DataField] public LocId JoinTitle = "mind-collective-consent-title";
    [DataField] public LocId JoinDescription = "mind-collective-consent-description";
    public EntityUid? Collective;
}

/// <summary>A body registered with one collective. It does not grant membership to its occupant.</summary>
[RegisterComponent]
public sealed partial class MindCollectiveBodyComponent : Component
{
    public EntityUid Collective;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MindCollectiveObserverComponent : Component
{
    public EntityUid Collective;
    public EntityUid? Listening;
    public TimeSpan OpenUiAt;
    [AutoNetworkedField] public EntityUid? Viewing;
    [AutoNetworkedField] public List<EntityUid> Bodies = new();
    [AutoNetworkedField] public List<MindCollectiveHearingState> Hearing = new();
    public override bool SendOnlyToOwner => true;
}

/// <summary>Private menu data for the player currently controlling this body.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MindCollectiveUiComponent : Component
{
    [AutoNetworkedField] public MindCollectiveUiState? State;
    public override bool SendOnlyToOwner => true;
}

public sealed partial class OpenMindCollectiveEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum MindCollectiveUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed record MindCollectiveBodyEntry(
    NetEntity Entity,
    string Name,
    string? Occupant,
    bool CanEnter,
    bool CanRecall,
    NetEntity? OccupantMind = null);

[Serializable, NetSerializable]
public sealed record MindCollectiveMemberEntry(NetEntity Mind, string Name, bool CanExpel);

[Serializable, NetSerializable]
public sealed class MindCollectiveUiState(
    List<MindCollectiveBodyEntry> bodies,
    List<MindCollectiveMemberEntry> members,
    bool owner,
    bool inMindspace,
    string name,
    NetEntity? expelTarget = null,
    uint expelRequest = 0) : BoundUserInterfaceState
{
    public readonly List<MindCollectiveBodyEntry> Bodies = bodies;
    public readonly List<MindCollectiveMemberEntry> Members = members;
    public readonly bool Owner = owner;
    public readonly bool InMindspace = inMindspace;
    public readonly string Name = name;
    public readonly NetEntity? ExpelTarget = expelTarget;
    public readonly uint ExpelRequest = expelRequest;
}

[Serializable, NetSerializable]
public sealed class EnterCollectiveBodyMessage(NetEntity body) : BoundUserInterfaceMessage
{
    public readonly NetEntity Body = body;
}

[Serializable, NetSerializable]
public sealed class ObserveCollectiveBodyMessage(NetEntity body) : BoundUserInterfaceMessage
{
    public readonly NetEntity Body = body;
}

[Serializable, NetSerializable]
public sealed class RecallCollectiveMemberMessage(NetEntity mind) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mind = mind;
}

[Serializable, NetSerializable]
public sealed class LeaveCollectiveBodyMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ExpelCollectiveMemberMessage(NetEntity mind, uint request = 0, bool confirmed = false) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mind = mind;
    public readonly uint Request = request;
    public readonly bool Confirmed = confirmed;
}
