using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Interaction.Events;

namespace Content.Shared._Starlight.MindCollective;

public sealed partial class MindCollectiveObserverSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnInteraction(Entity<MindCollectiveObserverComponent> ent, ref InteractionAttemptEvent args)
        => args.Cancelled = true;

    [SubscribeLocalEvent]
    private void OnConsciousAction(Entity<MindCollectiveObserverComponent> ent, ref ConsciousAttemptEvent args)
        => args.Cancelled = true;

    [SubscribeLocalEvent]
    private void OnCanSee(Entity<MindCollectiveObserverComponent> ent, ref CanSeeAttemptEvent args)
    {
        if (ent.Comp.Viewing is not { } body || TerminatingOrDeleted(body)
            || TryComp<BlindableComponent>(body, out var blind) && blind.IsBlind)
            args.Cancel();
    }
}
