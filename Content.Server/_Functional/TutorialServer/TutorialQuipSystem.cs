using Content.Shared._Functional.TutorialServer;
using Content.Shared.Nutrition;
using Robust.Shared.Containers;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Lets the coach remark on things never asked for.
/// </summary>
public sealed partial class TutorialQuipSystem : EntitySystem
{
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;
    [Dependency] private TutorialTrainerSystem _trainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialQuipComponent, IngestedEvent>(OnIngested);
        SubscribeLocalEvent<TutorialQuipComponent, EntInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnIngested(Entity<TutorialQuipComponent> ent, ref IngestedEvent args)
    {
        TrySpeak(ent, args.User, TutorialQuipTrigger.Ingested);
    }

    private void OnInserted(Entity<TutorialQuipComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!HasComp<TutorialParticipantComponent>(args.Entity))
            return;

        TrySpeak(ent, args.Entity, TutorialQuipTrigger.PlayerInserted);
    }

    private void TrySpeak(Entity<TutorialQuipComponent> ent, EntityUid player, TutorialQuipTrigger trigger)
    {
        if (!HasComp<TutorialParticipantComponent>(player))
            return;

        if (!_tutorial.TryGetSession(player, out var session))
            return;

        var mentor = session.MentorUid;
        if (mentor == EntityUid.Invalid || TerminatingOrDeleted(mentor))
            return;

        foreach (var quip in ent.Comp.Quips)
        {
            if (quip.Spoken || quip.Trigger != trigger)
                continue;

            quip.Spoken = true;
            _trainer.TrySpeakInterjection(mentor, player, quip.Line);
        }
    }
}
