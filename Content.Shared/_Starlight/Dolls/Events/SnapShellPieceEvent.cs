using Content.Shared.Actions;
using Content.Shared.Damage;

namespace Content.Shared._Starlight.Dolls.Events;

public sealed partial class SnapShellPieceEvent : InstantActionEvent
{

    /// <summary>
    /// Damage applied to user when used
    /// </summary>
    [DataField]
    public DamageSpecifier? SelfDamage;

    /// <summary>
    /// Should this require a free hand to work?
    /// </summary>
    [DataField]
    public bool RequiresFreeHand = false;
}
