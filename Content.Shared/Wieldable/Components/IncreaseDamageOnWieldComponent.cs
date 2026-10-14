using Content.Shared.Damage;

namespace Content.Shared.Wieldable.Components;

[RegisterComponent, Access(typeof(SharedWieldableSystem))]
public sealed partial class IncreaseDamageOnWieldComponent : Component
{
    [DataField("damage", required: true)]
    [Access(Other = AccessPermissions.ReadExecute)]
    public DamageSpecifier BonusDamage = default!;

    #region Starlight

    /// <summary>
    /// Whether to respect the active state of a toggleable item or not. (e.g. if item is deactivated, don't apply damage).
    /// </summary>
    [DataField] public bool RespectActiveState;

    #endregion
}
