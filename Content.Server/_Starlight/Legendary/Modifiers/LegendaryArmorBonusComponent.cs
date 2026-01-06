using Content.Shared.Damage;

namespace Content.Server._Starlight.Legendary.Modifiers;

[RegisterComponent]
public sealed partial class LegendaryArmorBonusComponent : Component
{
    [DataField]
    public DamageModifierSet Modifiers = new()
    {
        Coefficients = new Dictionary<string, float>
        {
            ["Blunt"] = 0.95f,
            ["Slash"] = 0.95f,
            ["Piercing"] = 0.95f,
            ["Heat"] = 0.95f,
        }
    };
}
