using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Interaction.Events;

namespace Content.Shared._Starlight.Changeling;

public sealed class ChangelingMindspaceSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangelingMindspaceComponent, InteractionAttemptEvent>(OnInteraction);
        SubscribeLocalEvent<ChangelingMindspaceComponent, ConsciousAttemptEvent>(OnConsciousAction);
        SubscribeLocalEvent<ChangelingMindspaceComponent, CanSeeAttemptEvent>(OnCanSee);
    }

    private void OnInteraction(Entity<ChangelingMindspaceComponent> ent, ref InteractionAttemptEvent args)
        => args.Cancelled = true;

    private void OnConsciousAction(Entity<ChangelingMindspaceComponent> ent, ref ConsciousAttemptEvent args)
        => args.Cancelled = true;

    private void OnCanSee(Entity<ChangelingMindspaceComponent> ent, ref CanSeeAttemptEvent args)
    {
        if (TryComp<BlindableComponent>(ent.Comp.Viewing, out var blind))
        {
            if (blind.IsBlind)
                args.Cancel();
        }
    }
}
