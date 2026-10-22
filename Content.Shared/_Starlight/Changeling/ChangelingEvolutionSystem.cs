using Content.Shared.Body.Part;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Damage.Systems;
using Content.Shared.Flash;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Starlight.Changeling;

public sealed class ChangelingEvolutionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangelingComponent, GetMeleeDamageEvent>(OnMeleeDamage);
        SubscribeLocalEvent<ChangelingComponent, DamageModifyEvent>(OnIncomingDamage);
        SubscribeLocalEvent<ChangelingEyesComponent, FlashAttemptEvent>(OnFlash);
    }

    private bool IsEvolvedTorso(EntityUid uid)
        => TryComp<ChangelingColonyMemberComponent>(uid, out var member) && member.IsFragment
           && TryComp<BodyPartComponent>(uid, out var part) && part.PartType == BodyPartType.Torso;

    private void OnMeleeDamage(Entity<ChangelingComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (!ent.Comp.Evolved || ent.Comp.Biomass < 1 || args.User != ent.Owner)
            return;
        if (ent.Comp.StrainedMusclesActive)
            args.Damage *= 1.25f;
        if (IsEvolvedTorso(ent))
            args.Damage *= 1.5f;
    }

    private void OnIncomingDamage(Entity<ChangelingComponent> ent, ref DamageModifyEvent args)
    {
        if (!ent.Comp.Evolved || ent.Comp.Biomass < 1)
            return;
        var coefficient = IsEvolvedTorso(ent) ? 0.75f : ent.Comp.StrainedMusclesActive ? 0.8f : 1f;
        if (coefficient == 1)
            return;
        args.Damage = args.Damage.Clone();
        foreach (var (type, damage) in args.Damage.DamageDict)
        {
            if (damage > 0)
                args.Damage.DamageDict[type] = damage * coefficient;
        }
    }

    private void OnFlash(Entity<ChangelingEyesComponent> ent, ref FlashAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
