using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.MindCollective;

/// <summary>Lives on the owner's mind, independently of its current body.</summary>
[RegisterComponent]
public sealed partial class MindCollectiveComponent : Component
{
    public EntityUid OwnerMind;
    public EntityUid? ActiveBody;
    public Dictionary<EntityUid, EntityUid> Minds = new();
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
}

/// <summary>Membership belongs to a mind, not to the body it happens to control.</summary>
[RegisterComponent]
public sealed partial class MindCollectiveMemberComponent : Component
{
    public EntityUid Collective;
}
