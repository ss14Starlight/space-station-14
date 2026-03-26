namespace Content.Server._Starlight.Paper;

[RegisterComponent]
public sealed partial class ChargesExamineComponent : Component
{
    /// <summary>
    /// what localization string should be used for this component
    /// </summary>
    [DataField]
    public LocId Loc = "component-chargeexamine-loc";

    /// <summary>
    /// what localization string should be used for this component
    /// </summary>
    [DataField]
    public LocId LocNoCharges = "component-chargeexamine-loc-finished";
}
