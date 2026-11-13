using Content.Shared.Actions;
using Content.Shared.Damage;

namespace Content.Shared._Starlight.Actions.Events;

public sealed partial class DamageActionEvent : EntityTargetActionEvent
{
    /// <summary>
    /// The damage specifier to apply to the target
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 2.25}
        }
    };

    /// <summary>
    /// Does the damage bypass resistances?
    /// </summary>
    [DataField]
    public bool IgnoreResistances = false;
}
