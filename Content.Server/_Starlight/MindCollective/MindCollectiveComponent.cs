using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.MindCollective;
using Content.Shared._Starlight.BodyControl;

namespace Content.Server._Starlight.MindCollective;

/// <summary>An independent group. Its lifetime is decided by the system using the framework.</summary>
[RegisterComponent]
public sealed partial class MindCollectiveComponent : Component
{
    public EntityUid? ActiveBody;
    public Dictionary<EntityUid, EntityUid> Minds = new();
    public Dictionary<EntityUid, Dictionary<ProtoId<MindCollectiveRolePrototype>, MindCollectiveRoleAssignment>> Roles = new();
    public HashSet<EntityUid> PendingMinds = new();
    public HashSet<EntityUid> VacantObservers = new();
    public HashSet<NetUserId> ExcludedUsers = new();
    public HashSet<EntityUid> Bodies = new();
    public bool Moving;
    public bool Closing;
    public TimeSpan NextUpdate;
    [DataField] public EntProtoId ObserverPrototype = "MindCollectiveObserver";
    [DataField] public EntProtoId? VacancyPrototype = "MindCollectiveVacancy";
    [DataField] public LocId JoinTitle = "mind-collective-consent-title";
    [DataField] public LocId JoinDescription = "mind-collective-consent-description";
    [DataField] public ProtoId<MindCollectiveRolePrototype> InitialRole = "MindCollectiveOwner";
    [DataField] public ProtoId<MindCollectiveRolePrototype> DefaultRole = "MindCollectiveMember";
    [DataField] public MindCollectiveDepartureDisposition DeclineDisposition = MindCollectiveDepartureDisposition.ReturnToPreviousBody;
    [DataField] public MindCollectiveDepartureDisposition ExpelDisposition = MindCollectiveDepartureDisposition.Detach;
    [DataField] public bool CreateVacancyOnDecline;
    [DataField] public bool CreateVacancyOnExpel;
    [DataField] public bool ExcludeExpelledUsers = true;
}

public enum MindCollectiveDepartureDisposition : byte { Detach, ReturnToPreviousBody, Ghost }

public sealed class MindCollectiveRoleAssignment
{
    public HashSet<EntityUid>? Bodies;
    public BodyControlAccess? Control;
}

/// <summary>Membership belongs to a mind, not to the body it happens to control.</summary>
[RegisterComponent]
public sealed partial class MindCollectiveMemberComponent : Component
{
    public EntityUid Collective;
}
