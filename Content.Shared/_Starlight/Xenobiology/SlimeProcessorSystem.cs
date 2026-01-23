using System.Numerics;
using Content.Shared.Coordinates;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Power;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Xenobiology;

public sealed class SlimeProcessorSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedJitteringSystem _jitteringSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeProcessorComponent, ActivateInWorldEvent>(OnAfterActivate);
        SubscribeLocalEvent<SlimeProcessorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SlimeProcessorComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerb);
    }

    private void OnAfterActivate(Entity<SlimeProcessorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (CanActivate(ent))
            EnableProcessingWrapper(ent);
    }

    private void OnGetVerb(Entity<SlimeProcessorComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;
        
        var itemVerb = new InteractionVerb();
        itemVerb.Text = Loc.GetString("comp-slime-processor-verb-activate");
        if (CanActivate(ent))
        {
            itemVerb.Message = Loc.GetString("comp-slime-processor-verb-activate-message-success");
        }
        else
        {
            itemVerb.Disabled = true;
            itemVerb.Message = Loc.GetString("comp-slime-processor-verb-activate-message-no-slimes");
        }
        itemVerb.Act = () => EnableProcessingWrapper(ent);
        args.Verbs.Add(itemVerb);
    }

    private void EnableProcessingWrapper(Entity<SlimeProcessorComponent> ent)
    {
        EnableProcessing(ent, _entityManager, _gameTiming);
        _jitteringSystem.AddJitter(ent.Owner, -10, 100);
        _audioSystem.PlayPredicted(new SoundPathSpecifier("/Audio/Machines/blender.ogg"), ent.Owner, null);
    }

    private bool CanActivate(Entity<SlimeProcessorComponent> ent) => ent.Comp.SlimeContainer.ContainedEntities.Count > 0 && !_entityManager.HasComponent<ActiveSlimeProcessorComponent>(ent.Owner);

    private void OnPowerChanged(Entity<SlimeProcessorComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            if (HasComp<ActiveSlimeProcessorComponent>(ent.Owner))
                RemCompDeferred<ActiveSlimeProcessorComponent>(ent.Owner);
            if (HasComp<CollectingSlimeProcessorComponent>(ent.Owner))
                RemCompDeferred<CollectingSlimeProcessorComponent>(ent.Owner);
            if (HasComp<JitteringComponent>(ent.Owner))
                RemCompDeferred<JitteringComponent>(ent.Owner);
        }
        else
        {
            EnableCollecting(ent, _entityManager, _gameTiming);
        }
    }

    public static void EnableCollecting(Entity<SlimeProcessorComponent> ent, EntityManager entityManager, IGameTiming gameTiming)
    {
        if (!entityManager.HasComponent<CollectingSlimeProcessorComponent>(ent.Owner))
        {
            if (entityManager.HasComponent<ActiveSlimeProcessorComponent>(ent))
                entityManager.RemoveComponentDeferred<ActiveSlimeProcessorComponent>(ent);
            var collectingSlimeProcessorComponent = entityManager.AddComponent<CollectingSlimeProcessorComponent>(ent.Owner);
            collectingSlimeProcessorComponent.SlimeAcquireMoment = gameTiming.CurTime + ent.Comp.SlimeAcquireCooldown;
        }
    }
    
    public static void EnableProcessing(Entity<SlimeProcessorComponent> ent, EntityManager entityManager, IGameTiming gameTiming)
    {
        if (!entityManager.HasComponent<ActiveSlimeProcessorComponent>(ent.Owner))
        {
            if (entityManager.HasComponent<CollectingSlimeProcessorComponent>(ent))
                entityManager.RemoveComponentDeferred<CollectingSlimeProcessorComponent>(ent);
            var activeSlimeProcessorComponent = entityManager.AddComponent<ActiveSlimeProcessorComponent>(ent);
            activeSlimeProcessorComponent.ProcessingFinishedMoment = gameTiming.CurTime + ent.Comp.ProcessingTime;
        }
    }
}

public sealed class ActiveSlimeProcessorSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveSlimeProcessorComponent>();
        while (query.MoveNext(out var uid, out var activeSlimeProcessorComponent))
        {
            if (!_entityManager.TryGetComponent(uid, out SlimeProcessorComponent? slimeProcessorComponent)) continue;
            if (!activeSlimeProcessorComponent.ProcessingFinishedMoment.HasValue)
            {
                activeSlimeProcessorComponent.ProcessingFinishedMoment = _gameTiming.CurTime + slimeProcessorComponent.ProcessingTime;
                continue;
            }

            if (activeSlimeProcessorComponent.ProcessingFinishedMoment.Value > _gameTiming.CurTime) continue;
            var random = _robustRandom.GetRandom();
            foreach (var entity in slimeProcessorComponent.SlimeContainer.ContainedEntities)
            {
                if (!_entityManager.TryGetComponent(entity, out SlimeComponent? slimeComponent)) continue;
                for (int i = 0; i < slimeProcessorComponent.YieldMultiplier + slimeComponent.SlimeSteroidAmount; i++)
                {
                    Vector2 randomOffset = new Vector2(random.NextFloat(-0.2F, 0.2F), random.NextFloat(-0.2F, 0.2F));
                    EntityCoordinates ec = new EntityCoordinates(uid, uid.ToCoordinates().Position + randomOffset);
                    _entityManager.PredictedSpawnAtPosition(slimeComponent.Extract, ec);
                }
                PredictedQueueDel(entity);
            }
            
            RemCompDeferred<JitteringComponent>(uid);
            RemCompDeferred<ActiveSlimeProcessorComponent>(uid);
            SlimeProcessorSystem.EnableCollecting((uid, slimeProcessorComponent), _entityManager, _gameTiming);
        }
    }
}

public sealed class CollectingSlimeProcessorSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CollectingSlimeProcessorComponent>();
        while (query.MoveNext(out var uid, out var collectingSlimeProcessorComponent))
        {
            if (!_entityManager.TryGetComponent(uid, out SlimeProcessorComponent? slimeProcessorComponent)) continue;
            slimeProcessorComponent.SlimeContainer = _container.EnsureContainer<Container>(uid, SlimeProcessorComponent.SlimeContainerName);
            if (!collectingSlimeProcessorComponent.SlimeAcquireMoment.HasValue)
            {
                collectingSlimeProcessorComponent.SlimeAcquireMoment = _gameTiming.CurTime + slimeProcessorComponent.SlimeAcquireCooldown;
                continue;
            }

            if (collectingSlimeProcessorComponent.SlimeAcquireMoment.Value > _gameTiming.CurTime) continue;
            foreach (var entity in _entityLookupSystem.GetEntitiesInRange<SlimeComponent>(Transform(uid).Coordinates, 1F))
            {
                if (_container.IsEntityOrParentInContainer(entity.Owner)) continue;
                if (!_entityManager.TryGetComponent(entity, out DamageableComponent? damageableComponent)) continue;
                if (damageableComponent.TotalDamage >= 200)
                {
                    _container.Insert(entity.Owner, slimeProcessorComponent.SlimeContainer);
                    collectingSlimeProcessorComponent.SlimeAcquireMoment = _gameTiming.CurTime + slimeProcessorComponent.SlimeAcquireCooldown;
                    break;
                }
            }
        }
    }
}