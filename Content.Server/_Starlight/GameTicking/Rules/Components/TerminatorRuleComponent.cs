namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class TerminatorRuleComponent : Component
{
    public EntityUid? Target;
    public EntityUid? TargetBody;
    [DataField]
    public string TerminatorEntityPrototype = "MobHumanTerminator";
    [DataField]
    public string PinpointerPrototype = "PinpointerTerminator";
    [DataField]
    public string SpawnEffectPrototype = "EffectTerminatorChronospace";
}
