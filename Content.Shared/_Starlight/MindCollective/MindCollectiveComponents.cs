using Content.Shared.Actions;
using Content.Shared._Starlight.BodyControl;
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
    [DataField] public EntProtoId CollectivePrototype = "MindCollective";
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
    [AutoNetworkedField] public bool AllowWorldInteraction;
    [AutoNetworkedField] public bool AllowConsciousActions;
    [AutoNetworkedField] public bool AllowWorldSpeech;
    [AutoNetworkedField] public bool AllowWorldPointing;
    public override bool SendOnlyToOwner => true;
}

/// <summary>Private menu data for the player currently controlling this body.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MindCollectiveUiComponent : Component
{
    public EntityUid? EditingMind;
    public EntityUid? EditingBody;
    [AutoNetworkedField] public MindCollectiveUiState? State;
    public override bool SendOnlyToOwner => true;
}

public sealed partial class OpenMindCollectiveEvent : InstantActionEvent, IBodyControlPersonalAction;

[Serializable, NetSerializable]
public enum MindCollectiveUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed record MindCollectiveBodyEntry(
    NetEntity Entity,
    string Name,
    string? Occupant,
    bool CanEnter,
    bool CanRecall,
    NetEntity? OccupantMind = null,
    bool CanControl = false,
    bool IsControlling = false);

[Serializable, NetSerializable]
public sealed record MindCollectiveMemberEntry(NetEntity Mind, string Name, bool CanExpel,
    List<MindCollectiveRoleEntry> Roles, bool CanManageRoles, bool CanRecall, bool CanConfigureControl = false);

[Serializable, NetSerializable]
public sealed record MindCollectiveControlEditorState(NetEntity Mind, NetEntity Body, string Name, BodyControlAccessState Settings);

[Serializable, NetSerializable]
public sealed record MindCollectiveRoleEntry(ProtoId<MindCollectiveRolePrototype> Role, string Name, bool Assigned);

[Serializable, NetSerializable]
public sealed class MindCollectiveUiState(
    List<MindCollectiveBodyEntry> bodies,
    List<MindCollectiveMemberEntry> members,
    bool canManage,
    bool inMindspace,
    string name,
    NetEntity? expelTarget = null,
    uint expelRequest = 0,
    MindCollectiveControlEditorState? controlEditor = null) : BoundUserInterfaceState
{
    public readonly List<MindCollectiveBodyEntry> Bodies = bodies;
    public readonly List<MindCollectiveMemberEntry> Members = members;
    public readonly bool CanManage = canManage;
    public readonly bool InMindspace = inMindspace;
    public readonly string Name = name;
    public readonly NetEntity? ExpelTarget = expelTarget;
    public readonly uint ExpelRequest = expelRequest;
    public readonly MindCollectiveControlEditorState? ControlEditor = controlEditor;
}

[Serializable, NetSerializable]
public sealed class ConfigureCollectiveControlMessage(NetEntity? mind, NetEntity? body = null) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Mind = mind;
    public readonly NetEntity? Body = body;
}

[Serializable, NetSerializable]
public sealed class SetCollectiveControlMessage(NetEntity mind, NetEntity body, BodyControlAccess? access) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mind = mind;
    public readonly NetEntity Body = body;
    public readonly BodyControlAccess? Access = access;
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
public sealed class ControlCollectiveBodyMessage(NetEntity body) : BoundUserInterfaceMessage
{
    public readonly NetEntity Body = body;
}

[Serializable, NetSerializable]
public sealed class SetCollectiveRoleMessage(NetEntity mind, ProtoId<MindCollectiveRolePrototype> role, bool assigned) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mind = mind;
    public readonly ProtoId<MindCollectiveRolePrototype> Role = role;
    public readonly bool Assigned = assigned;
}

[Serializable, NetSerializable]
public sealed class ExpelCollectiveMemberMessage(NetEntity mind, uint request = 0, bool confirmed = false) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mind = mind;
    public readonly uint Request = request;
    public readonly bool Confirmed = confirmed;
}
