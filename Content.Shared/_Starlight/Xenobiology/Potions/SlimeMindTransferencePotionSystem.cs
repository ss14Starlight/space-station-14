using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed partial class SlimeMindTransferencePotionSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _sharedMindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeMindTransferencePotionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SlimeMindTransferencePotionComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach)
            return;

        args.Handled = true;

        // The target entity must NOT have a mind, but still able to possess a mind.
        if (!TryComp<MindContainerComponent>(args.User, out var userMindContainerComponent))
            return;

        if (!TryComp<MindContainerComponent>(target, out var targetMindContainerComponent))
            return;

        if (userMindContainerComponent.Mind is not { } mind)
            return;

        if (targetMindContainerComponent.HasMind)
            return;

        _sharedMindSystem.TransferTo(mind, target);
        PredictedQueueDel(args.Used);
    }
}
