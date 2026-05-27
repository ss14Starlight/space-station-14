using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Interaction.Components;
using Content.Shared.Humanoid;
using Content.Shared.Body.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Wieldable;
using Content.Shared._Starlight.Medical.Body.Systems;

namespace Content.Server._Starlight.Antags.Vampires.Systems;

/// <summary>
/// Handles vampiric claws lifecycle and effects
/// </summary>
public sealed class VampiricClawsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VampiricClawsComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<VampiricClawsComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<VampiricClawsComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<VampiricClawsComponent, ItemUnwieldedEvent>(OnUnwielded);
    }

    private void OnInit(Entity<VampiricClawsComponent> ent, ref MapInitEvent args) => EnsureComp<UnremoveableComponent>(ent);

    private void OnUseInHand(Entity<VampiricClawsComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (TryComp<VampireComponent>(args.User, out var vamp))
            ClearClawsReference(ent.Owner, vamp);

        _popup.PopupEntity(Loc.GetString("vampiric-claws-remove-popup"), ent.Owner, args.User);

        QueueDel(ent);
    }

    private void OnMeleeHit(Entity<VampiricClawsComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var bloodGained = 0;
        foreach (var hitEntity in args.HitEntities)
            if (HasComp<HumanoidAppearanceComponent>(hitEntity)
                && TryComp<BloodstreamComponent>(hitEntity, out var victimBlood)
                && _bloodstream.TryModifyBloodLevel((hitEntity, victimBlood), -ent.Comp.BloodPerHit))
                bloodGained += ent.Comp.BloodPerHit;

        if (bloodGained > 0 && TryComp<VampireComponent>(args.User, out var vamp))
        {
            vamp.DrunkBlood += bloodGained;
            vamp.TotalBlood += bloodGained;

            vamp.BloodFullness = MathF.Min(vamp.MaxBloodFullness, vamp.BloodFullness + bloodGained);
            Dirty(args.User, vamp);

            RaiseLocalEvent(args.User, new VampireProgressionChangedEvent());

            if (TryComp<HungerComponent>(args.User, out var hunger))
                _hunger.ModifyHunger(args.User, bloodGained * 2, hunger);

            ent.Comp.HitsRemaining--;
            Dirty(ent);
            if (ent.Comp.HitsRemaining <= 0)
            {
                ClearClawsReference(ent.Owner, vamp);
                QueueDel(ent);
            }
        }
    }

    private void ClearClawsReference(EntityUid claws, VampireComponent vampire)
        => vampire.SpawnedClaws = vampire.SpawnedClaws == claws ? null : vampire.SpawnedClaws;

    private void OnUnwielded(Entity<VampiricClawsComponent> ent, ref ItemUnwieldedEvent args)
    {
        if (TryComp<VampireComponent>(args.User, out var vampire))
        {
            if (vampire.SpawnedClaws != ent.Owner)
                return;

            vampire.SpawnedClaws = null;
        }

        QueueDel(ent);
    }
}
