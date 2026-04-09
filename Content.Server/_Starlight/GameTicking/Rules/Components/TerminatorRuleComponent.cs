namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class TerminatorRuleComponent : Component
{
    public EntityUid? Target;
    public EntityUid? TargetBody;
}
