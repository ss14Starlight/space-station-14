using Content.Server.Buckle.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Server.Stunnable;
using Content.Shared._Moffstation.Chasm;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Chasm;
using Content.Shared.Destructible;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.Chasm;

/// <summary>
///     Handles making entities fall into chasms when stepped on.
/// </summary>
public sealed class ChasmSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityStorageSystem _entStorage = default!;
    [Dependency] private readonly BuckleSystem _buckle = default!;
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly StunSystem _stun = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ChasmComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<ChasmComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ChasmComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<ChasmFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    private void OnComponentInit(Entity<ChasmComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Hole =
            _containerSystem.EnsureContainer<Container>(ent, ent.Comp.HoleContainerId);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ChasmComponent>();

        while (query.MoveNext(out _, out var chasm))
        {
            foreach (var entity in chasm.FallingObjects)
            {
                if (!TryComp<ChasmFallingComponent>(entity, out var falling) ||
                    falling.NextDeletionTime > _timing.CurTime)
                    continue;

                chasm.FallingObjects.Remove(entity);
                RemCompDeferred<ChasmFallingComponent>(entity);

                // If it isn't set to be stored, then delete it
                if (_whitelist.IsBlacklistPass(chasm.PreservationBlacklist, entity) ||
                    !_whitelist.IsWhitelistPass(chasm.PreservationWhitelist, entity))
                {
                    TryQueueDel(entity);
                    continue;
                }

                if (TryComp<MobStateComponent>(entity, out var mobState))
                {
                    _mobState.ChangeMobState(entity, MobState.Dead, mobState);
                    EnsureComp<StunnedComponent>(entity);
                }

                _containerSystem.Insert(entity, chasm.Hole);
            }
        }
    }

    private void OnDestroyed(Entity<ChasmComponent> ent, ref DestructionEventArgs args)
    {
        foreach (var uid in _containerSystem.EmptyContainer(ent.Comp.Hole))
        {
            RemCompDeferred<StunnedComponent>(uid);
            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(2), false);
        }

        var query = EntityQueryEnumerator<ChasmFallingComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (ent.Comp.FallingObjects.Contains(uid))
                RemCompDeferred<ChasmFallingComponent>(uid);
        }
    }

    private void OnStepTriggered(Entity<ChasmComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (HasComp<ChasmFallingComponent>(args.Tripper))
            return;

        if (TryComp<PullableComponent>(args.Tripper, out var pullable) && !pullable.BeingPulled)
            _pulling.TryStopPull(args.Tripper, pullable);

        // Reject if blacklisted.
        if (_whitelist.IsBlacklistPass(ent.Comp.Blacklist, args.Tripper))
        {
            if (!ent.Comp.ThrowBlacklisted)
                return;
            _throwing.TryThrow(args.Tripper, _random.NextVector2() * 10, 7);
            return;
        }

        // Whether living mobs will fall in
        if (!ent.Comp.AllowLiving && _mobState.IsAlive(args.Tripper))
            return;

        // Whether we need to empty a container before putting it into the hole
        if (ent.Comp.DumpContainers)
            _entStorage.EmptyContents(args.Tripper);

        if (TryComp<StrapComponent>(args.Tripper, out var strapComp) && strapComp.BuckledEntities.Count > 0)
        {
            foreach (var buckled in strapComp.BuckledEntities)
            {
                _buckle.TryUnbuckle(buckled, null, popup:false);
            }
        }

        StartFalling(ent, args.Tripper);
    }

    private void StartFalling(Entity<ChasmComponent> ent, EntityUid tripper, bool playSound = true)
    {
        var falling = AddComp<ChasmFallingComponent>(tripper);

        var ev = new ChasmFallEvent(ent,  tripper);
        RaiseLocalEvent(ent, ref ev);

        //Stop the object from being pulled
        if (TryComp<PullableComponent>(tripper, out var pullable))
            _pulling.TryStopPull(tripper, pullable);

        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(tripper);

        ent.Comp.FallingObjects.Add(tripper);

        if (playSound)
            _audio.PlayPredicted(ent.Comp.FallingSound, ent, tripper);
    }

    private void OnStepTriggerAttempt(EntityUid uid, ChasmComponent component, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    private void OnUpdateCanMove(EntityUid uid, ChasmFallingComponent component, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }
}

/// <summary>
/// Raised when an entity falls into a chasm
/// </summary>
[ByRefEvent]
public readonly record struct ChasmFallEvent(EntityUid Source, EntityUid Tripper);
