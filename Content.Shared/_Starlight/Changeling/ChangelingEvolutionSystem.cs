using Content.Shared.Body.Part;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Damage.Systems;
using Content.Shared.Flash;
using Content.Shared.Prying.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Changeling;

public sealed class ChangelingEvolutionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangelingComponent, GetMeleeDamageEvent>(OnMeleeDamage);
        SubscribeLocalEvent<ChangelingComponent, DamageModifyEvent>(OnIncomingDamage);
        SubscribeLocalEvent<ChangelingEyesComponent, FlashAttemptEvent>(OnFlash);
        SubscribeLocalEvent<ChangelingComponent, AfterAutoHandleStateEvent>(OnStrengthState);
        SubscribeLocalEvent<ChangelingComponent, ComponentShutdown>(OnStrengthShutdown);
    }

    /// <summary>
    /// Applies or removes the body's temporary ability to force doors open.
    /// </summary>
    public void RefreshStrength(Entity<ChangelingComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;
        SetStrengthPrying((ent.Owner, ent.Comp), ent.Comp.StrainedMusclesActive);
    }

    private void OnStrengthState(Entity<ChangelingComponent> ent, ref AfterAutoHandleStateEvent args)
        => SetStrengthPrying(ent, ent.Comp.StrainedMusclesActive);

    private void OnStrengthShutdown(Entity<ChangelingComponent> ent, ref ComponentShutdown args)
        => SetStrengthPrying(ent, false);

    private void SetStrengthPrying(Entity<ChangelingComponent> ent, bool active)
    {
        if (active)
        {
            if (TryComp<PryingComponent>(ent, out var prying))
            {
                if (prying == ent.Comp.StrengthPrying)
                    return;
                ent.Comp.OriginalStrengthPrying = (prying.Enabled, prying.PryPowered, prying.Force, prying.SpeedModifier);
            }
            else
            {
                prying = AddComp<PryingComponent>(ent);
                ent.Comp.OriginalStrengthPrying = null;
            }
            ent.Comp.StrengthPrying = prying;
            prying.Enabled = true;
            prying.PryPowered = true;
            prying.Force = true;
            prying.SpeedModifier = Math.Max(prying.SpeedModifier, 10f);
            return;
        }

        if (ent.Comp.StrengthPrying is { } owned && TryComp<PryingComponent>(ent, out var current) && current == owned)
        {
            if (ent.Comp.OriginalStrengthPrying is { } original)
            {
                current.Enabled = original.Enabled;
                current.PryPowered = original.PryPowered;
                current.Force = original.Force;
                current.SpeedModifier = original.SpeedModifier;
            }
            else
                RemComp<PryingComponent>(ent);
        }
        ent.Comp.StrengthPrying = null;
        ent.Comp.OriginalStrengthPrying = null;
    }

    private bool IsEvolvedTorso(EntityUid uid)
        => TryComp<ChangelingColonyMemberComponent>(uid, out var member) && member.IsFragment
           && TryComp<BodyPartComponent>(uid, out var part) && part.PartType == BodyPartType.Torso;

    private void OnMeleeDamage(Entity<ChangelingComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.Biomass < 1 || args.User != ent.Owner)
            return;
        if (ent.Comp.StrainedMusclesActive)
            args.Damage *= ent.Comp.Evolved ? 3f : 2f;
        if (ent.Comp.Evolved && IsEvolvedTorso(ent))
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
