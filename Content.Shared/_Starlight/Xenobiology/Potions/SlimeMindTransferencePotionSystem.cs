using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeMindTransferencePotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedMindSystem _sharedMindSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeMindTransferencePotionComponent, AfterInteractEvent>(OnAfterInteract);
    }
    
    private void OnAfterInteract(Entity<SlimeMindTransferencePotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<MindContainerComponent>(args.User,
                out _)) return;
        _sharedMindSystem.TransferTo(ent.Owner, args.Target.Value);
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}