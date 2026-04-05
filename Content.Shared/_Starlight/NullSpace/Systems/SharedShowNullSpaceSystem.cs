using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Actions;
using Content.Shared.Interaction.Events;

namespace Content.Shared._Starlight.NullSpace;

public abstract partial class SharedShowNullSpaceSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    private const string ActionCosmicBlankId = "ActionCosmicBlank";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShowNullSpaceComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<ShowNullSpaceComponent, AttackAttemptEvent>(OnAttackAttempt);

        SubscribeLocalEvent<CosmicCultComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<CosmicCultComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(EntityUid uid, ShowNullSpaceComponent component, AttackAttemptEvent args)
    {
        if (HasComp<NullSpaceComponent>(args.Target))
            args.Cancel();
    }

    private void OnAttackAttempt(EntityUid uid, CosmicCultComponent component, AttackAttemptEvent args)
    {
        if (HasComp<NullSpaceComponent>(args.Target))
            args.Cancel();
    }

    private void OnInteractionAttempt(EntityUid uid, ShowNullSpaceComponent component, ref InteractionAttemptEvent args)
    {
        if (HasComp<NullSpaceComponent>(args.Target))
            args.Cancelled = true;
    }

    private void OnInteractionAttempt(EntityUid uid, CosmicCultComponent component, ref InteractionAttemptEvent args)
    {
        if (HasComp<NullSpaceComponent>(args.Target))
            args.Cancelled = true;
    }
}
