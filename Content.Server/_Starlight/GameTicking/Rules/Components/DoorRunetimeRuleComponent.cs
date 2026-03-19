using Content.Server.StationEvents.Events;
using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(DoorRunetimeRule))]
public sealed partial class DoorRunetimeRuleComponent : Component
{
    public readonly HashSet<EntityUid> AffectedEntities = new();

    [DataField]
    public List<ProtoId<AccessLevelPrototype>> Blacklist = new();
}