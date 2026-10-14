using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Doomed;

[RegisterComponent, NetworkedComponent]
public sealed partial class DoomedComponent : Component
{
    /// <summary>
    /// How long till they die?
    /// </summary>
    [DataField]
    public TimeSpan TimeToDeath = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Damage to deal upon the timer running out
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 10000 }
        }
    };

    [DataField]
    public EntProtoId DamageEffect = "EffectFlashDragonDisappear";

    /// <summary>
    /// Alert to display to the player
    /// </summary>
    [DataField]
    public EntProtoId StatusEffect = "StatusEffectDoomedIcon";

    public EntityUid StatusEffectUid;
}
