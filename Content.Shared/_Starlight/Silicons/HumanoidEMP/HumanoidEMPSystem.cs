// Humanoid EMP System
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;

namespace Content.Shared._Starlight.Silicons.HumanoidEMP;

/// <summary>
/// System that applies EMP effects to humanoid silicons.
/// </summary>
public sealed class HumanoidEMPSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<HumanoidEMPComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnEmpPulse(Entity<HumanoidEMPComponent> ent, ref EmpPulseEvent args)
    {
        if (args.Affected)
            return;

        var comp = ent.Comp;
        var multiplier = comp.EffectMultiplier;

        // Apply stun
        if (comp.StunTime > 0)
        {
            var stunTime = TimeSpan.FromSeconds(comp.StunTime * multiplier);
            _stun.TryAddParalyzeDuration(ent, stunTime);
        }

        // Apply knockdown
        if (comp.KnockdownTime > 0)
        {
            var knockdownTime = TimeSpan.FromSeconds(comp.KnockdownTime * multiplier);
            _stun.TryKnockdown(ent.Owner, knockdownTime, refresh: true);
        }

        // Slowdown would need to be implemented through MovementModStatusSystem
        // if (comp.SlowdownTime > 0)
        // {
        //     var slowdownTime = TimeSpan.FromSeconds(comp.SlowdownTime * multiplier);
        //     // Would require MovementModStatusSystem dependency
        // }

        // Apply damage
        if (comp.Damage != null && TryComp<DamageableComponent>(ent, out var damageable))
        {
            var scaledDamage = comp.Damage * multiplier;
            _damageable.TryChangeDamage((ent, damageable), scaledDamage, origin: args.User);
        }

        // Drop all held items
        if (comp.DropHeldItems)
        {
            _hands.TryDrop(ent.Owner);
        }

        // Apply status effects
        foreach (var (effectId, duration) in comp.StatusEffects)
        {
            var scaledDuration = duration * multiplier;
            _statusEffects.TryAddStatusEffectDuration(ent, effectId, scaledDuration);
        }

        args.Affected = true;
    }
}
