namespace Content.Server._Starlight.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicChaplainConditionComponent : Component
{
    /// <summary>
    ///     The amount of Chaplains this objective would like to be converted
    /// </summary>
    [DataField]
    public int Converted;
}
