using Content.Shared.Damage.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Electrocution;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;

namespace Content.Shared._Starlight.Changeling;

public sealed partial class ChangelingPainSuppressionSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnSlowdown(Entity<ChangelingPainSuppressionComponent> ent, ref ModifySlowOnDamageSpeedEvent args)
    {
        args.Speed = 1;
    }

    [SubscribeLocalEvent]
    private void OnStaminaDamage(Entity<ChangelingPainSuppressionComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (args.Value > 0)
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnDamage(Entity<ChangelingPainSuppressionComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Damage.DamageDict.TryGetValue("Shock", out var shock) && shock > 0)
        {
            args.Damage = args.Damage.Clone();
            args.Damage.DamageDict.Remove("Shock");
        }
    }

    [SubscribeLocalEvent]
    private void OnStatusEffect(Entity<ChangelingPainSuppressionComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (args.Effect == SharedStunSystem.StunId || args.Effect == SharedStaminaSystem.StaminaLow)
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnElectrocution(Entity<ChangelingPainSuppressionComponent> ent, ref ElectrocutionAttemptEvent args)
    {
        args.Cancel();
    }
}
