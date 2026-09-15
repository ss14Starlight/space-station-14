using System.Linq;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Combat.OnHit;

public abstract partial class SharedOnHitSystem : EntitySystem
{
    [Dependency] protected INetManager _net = default!;
    [Dependency] protected SharedDoAfterSystem _doAfter = default!;
    [Dependency] protected SharedAudioSystem _audio = default!;
    [Dependency] protected ReactiveSystem _reactiveSystem = default!;
    [Dependency] protected SharedSolutionContainerSystem _solutionContainers = default!;
    [Dependency] protected SharedColorFlashEffectSystem _color = default!;
    [Dependency] protected SharedCuffableSystem _cuffs = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<InjectOnHitComponent, MeleeHitEvent>(OnInjectOnMeleeHit);
        SubscribeLocalEvent<CuffsOnHitComponent, MeleeHitEvent>(OnCuffsOnMeleeHit);

        base.Initialize();
    }

    private void OnCuffsOnMeleeHit(Entity<CuffsOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit
         || !args.HitEntities.Any())
            return;

        var ev = new InjectOnHitAttemptEvent(false, args.User);
        RaiseLocalEvent(ent, ref ev);
        if (ev.Cancelled)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!TryComp<CuffableComponent>(target, out var cuffable) || cuffable.Container.Count != 0)
                continue;
            var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.Duration, new CuffsOnHitDoAfter(), ent, target)
            {
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
                BreakOnDamage = true,
                NeedHand = true,
                DistanceThreshold = 1f
            };

            if (!_doAfter.TryStartDoAfter(doAfterEventArgs))
                continue;
            _color.RaiseEffect(Color.FromHex("#601653"), new List<EntityUid>(1) { target }, Filter.Pvs(target, entityManager: EntityManager));
        }
    }

    private void OnInjectOnMeleeHit(Entity<InjectOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit
            || !args.HitEntities.Any())
            return;

        var ev = new InjectOnHitAttemptEvent(false, args.User);
        RaiseLocalEvent(ent, ref ev);
        if (ev.Cancelled)
            return;

        foreach (var target in args.HitEntities)
        {
            // Check for target-specific immunity (like hardsuit immunity)
            var targetAttempt = new InjectOnHitAttemptEvent(false, args.User);
            RaiseLocalEvent(target, ref targetAttempt, true);
            if (targetAttempt.Cancelled)
                continue;

            if (_solutionContainers.TryGetInjectableSolution(target, out var targetSoln, out var targetSolution))
            {
                var solution = new Solution(ent.Comp.Reagents);

                foreach (var reagent in ent.Comp.Reagents)
                    if (ent.Comp.ReagentLimit != null && _solutionContainers.GetTotalPrototypeQuantity(target, reagent.Reagent.ToString()) >= FixedPoint2.New(ent.Comp.ReagentLimit.Value))
                        return;

                _reactiveSystem.DoEntityReaction(target, solution, ReactionMethod.Injection);
                _solutionContainers.TryAddSolution(targetSoln.Value, solution);
                _color.RaiseEffect(Color.FromHex("#0000FF"), new List<EntityUid>(1) { target }, Filter.Pvs(target, entityManager: EntityManager));
            }
            if (ent.Comp.Sound is not null && _net.IsServer)
                _audio.PlayPvs(ent.Comp.Sound, target);
        }
    }

    public override void Update(float frameTime)
    {
    }
}
