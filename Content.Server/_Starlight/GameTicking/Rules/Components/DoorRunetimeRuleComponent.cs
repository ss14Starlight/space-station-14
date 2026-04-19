using Content.Server.StationEvents.Events;
using Content.Shared.Whitelist;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(DoorRunetimeRule))]
public sealed partial class DoorRunetimeRuleComponent : Component
{
    public readonly HashSet<EntityUid> AffectedEntities = new();

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}
