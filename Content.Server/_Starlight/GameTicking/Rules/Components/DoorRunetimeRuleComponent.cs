using Content.Server.StationEvents.Events;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(DoorRunetimeRule))]
public sealed partial class DoorRunetimeRuleComponent : Component
{
    public readonly HashSet<EntityUid> AffectedEntities = new();
}