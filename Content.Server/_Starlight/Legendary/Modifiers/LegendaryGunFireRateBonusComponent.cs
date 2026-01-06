namespace Content.Server._Starlight.Legendary.Modifiers;

[RegisterComponent]
public sealed partial class LegendaryGunFireRateBonusComponent : Component
{
    [DataField]
    public float FireRateBonus = 1f;
}
