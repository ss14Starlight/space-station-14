namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicRiftHealthComponent : Component
{
    /// <summary>
    /// The amount of corpse-based health bonus currently applied to the rift.
    /// </summary>
    [DataField]
    public int AppliedCorpseBonus = 0;

    /// <summary>
    /// The maximum-health bonus granted for each stored corpse.
    /// </summary>
    [DataField]
    public int HealthPerCorpse = 5;
}
