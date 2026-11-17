// ReSharper disable CheckNamespace
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;

namespace Content.Server.ImmovableRod;

public sealed partial class ImmovableRodSystem
{
    [Dependency] private MobStateSystem _mobState = default!;

    /// <summary>
    /// Damages a mob the rod hit and logs it. A wizard in Rod Form is the rod, so their player shows as the attacker.
    /// </summary>
    private void DamageAndLog(EntityUid rod, EntityUid victim, DamageSpecifier damage)
    {
        var wasDead = _mobState.IsDead(victim);

        if (!_damageable.TryChangeDamage(victim, damage, out var dealt, ignoreResistances: true))
            return;

        if (!wasDead && _mobState.IsDead(victim))
        {
            _adminLogger.Add(LogType.Damaged, LogImpact.High,
                $"{ToPrettyString(rod):user} rodded {ToPrettyString(victim):target} to death for {dealt.GetTotal():damage} damage");
            return;
        }

        _adminLogger.Add(LogType.Damaged, LogImpact.Medium,
            $"{ToPrettyString(rod):user} rodded {ToPrettyString(victim):target} for {dealt.GetTotal():damage} damage");
    }
}
