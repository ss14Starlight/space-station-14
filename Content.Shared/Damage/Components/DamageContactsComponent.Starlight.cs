using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

public sealed partial class DamageContactsComponent
{
    /// <summary>
    /// Whether this entity can damage mobs that are already dead,
    /// set to true if you want to.
    /// </summary>
    [DataField("damageDead")]
    public bool DamageDead = true;
}
