namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(DoorRuntimeRule))]
public sealed partial class DoorRuntimeRuleComponent : Component
{
    public readonly HashSet<EntityUid> AffectedEntities = new();
}
