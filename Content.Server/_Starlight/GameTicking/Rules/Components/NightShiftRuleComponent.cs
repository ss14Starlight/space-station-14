namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(NightShiftRule))]
public sealed partial class NightShiftRuleComponent : Component
{
    /// <summary>
    /// The station whose night shift this event is forcing.
    /// </summary>
    [ViewVariables]
    public EntityUid? Station;
}
