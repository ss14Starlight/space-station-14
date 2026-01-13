using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeSentiencePotionComponentSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _sharedMindSystem = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeSentiencePotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeSentiencePotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<MindContainerComponent>(args.Target.Value, out _)) return;
        _sharedMindSystem.MakeSentient(args.Target.Value); // I hope this creates the associated ghost role because otherwise I've got nothing.
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}