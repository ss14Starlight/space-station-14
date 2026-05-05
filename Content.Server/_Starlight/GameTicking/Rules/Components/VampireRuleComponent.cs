namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class VampireRuleComponent : Component
{
    public readonly List<EntityUid> VampireMinds = new();
}
