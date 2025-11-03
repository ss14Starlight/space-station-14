using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Doomed;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DoomedComponent : Component
{
    /// <summary>
    /// How long till they die?
    /// </summary>
    [DataField]
    public TimeSpan TimeToDeath = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Damage to deal upon the timer running out
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 1000 }
        }
    };

    [DataField]
    public EntProtoId DamageEffect = "EffectFlashDragonDisappear";

    /// <summary>
    /// Alert to display to the player
    /// </summary>
    [DataField]
    public EntProtoId StatusEffect = "StatusEffectDoomedIcon";

    /// <summary>
    /// When was the component applied?
    /// Used to calculate when to explode the player
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan TimeApplied = default!;
}