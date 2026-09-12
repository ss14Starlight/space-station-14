using Content.Shared._Functional.TutorialServer;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Bound UI for the handheld Tutorial prompt (travel roles): checklist with Next/Hint, plus IC speech.
/// Speaks once per sub-goal change (not on a timer); muted while the guide UI is open.
/// When a mentor is in earshot, the mentor owns dialogue — this tablet stays quiet.
/// </summary>
public sealed partial class TutorialGuideSystem : EntitySystem
{
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;
    [Dependency] private TutorialTrainerSystem _coach = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialGuideComponent, BeforeActivatableUIOpenEvent>(OnBeforeOpen);
        SubscribeLocalEvent<TutorialGuideComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<TutorialParticipantComponent, TutorialParticipantProgressChangedEvent>(OnProgressChanged);

        Subs.BuiEvents<TutorialGuideComponent>(TutorialPromptUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<TutorialPromptNextBuiMsg>(OnNext);
            subs.Event<TutorialPromptHintBuiMsg>(OnHint);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var guides = EntityQueryEnumerator<TutorialGuideComponent, TransformComponent>();
        while (guides.MoveNext(out var guideUid, out var guide, out _))
        {
            if (!TryGetHolderParticipant(guideUid, out var playerUid, out var part))
                continue;

            if (!_coach.TryResolveDialogue(guideUid, trainer: null, playerUid, part, out var subGoalId, out var dialogue))
                continue;

            // Only speak when the sub-goal changes — no timed reminders.
            if (guide.LastSpokenSubGoal == subGoalId)
                continue;

            // Don't pile coach speech on top of an open tutorial prompt.
            // Hybrid cargo: mentor speaks while in range — tablet stays quiet.
            if (_ui.IsUiOpen(guideUid, TutorialPromptUiKey.Key) ||
                _tutorial.IsMentorCoachingInRange(playerUid))
            {
                guide.LastSpokenSubGoal = subGoalId;
                Dirty(guideUid, guide);
                continue;
            }

            SpeakGuide(guideUid, guide, playerUid, subGoalId, dialogue);
        }
    }

    private void OnProgressChanged(
        Entity<TutorialParticipantComponent> ent,
        ref TutorialParticipantProgressChangedEvent args)
    {
        OnParticipantProgressChanged(ent.Owner, args.GuideUid, args.OldGoalIndex, args.OldProgressIndex);
    }

    private void OnOpenAttempt(Entity<TutorialGuideComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<TutorialParticipantComponent>(args.User))
            args.Cancel();
    }

    private void OnBeforeOpen(Entity<TutorialGuideComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        SnapViewToProgress(ent, args.User);
        UpdateUi(ent, args.User);
    }

    private void OnOpened(Entity<TutorialGuideComponent> ent, ref BoundUIOpenedEvent args)
    {
        SnapViewToProgress(ent, args.Actor);
        UpdateUi(ent, args.Actor);
    }

    private void OnNext(Entity<TutorialGuideComponent> ent, ref TutorialPromptNextBuiMsg args)
    {
        TryGoNext(ent, args.Actor);
    }

    private void OnHint(Entity<TutorialGuideComponent> ent, ref TutorialPromptHintBuiMsg args)
    {
        TryShowStuckHint(args.Actor);
    }

    /// <summary>
    /// Shows the authored stuck hint in chat without advancing curriculum.
    /// </summary>
    public bool TryShowStuckHint(EntityUid user)
    {
        if (!TryComp<TutorialParticipantComponent>(user, out var part))
            return false;

        if (string.IsNullOrEmpty(part.StuckHintText))
            return false;

        _tutorial.SendTipChat(user, part.StuckHintText);
        return true;
    }

    /// <summary>
    /// Advances an Acknowledge tip. View is always snapped to live progress (no history paging).
    /// </summary>
    public bool TryGoNext(Entity<TutorialGuideComponent> ent, EntityUid user)
    {
        if (!TryComp<TutorialParticipantComponent>(user, out var part))
            return false;

        if (part.StepComplete != TutorialStepComplete.Acknowledge)
            return false;

        _tutorial.AdvanceSubGoal(user);
        SnapViewToProgress(ent, user);
        UpdateUi(ent, user);
        return true;
    }

    public TutorialPromptBuiState GetUiState(Entity<TutorialGuideComponent> ent, EntityUid user)
    {
        return BuildState(ent, user);
    }

    /// <summary>
    /// Keeps an open guide UI in sync when curriculum progress changes (sensors, etc.).
    /// Sends a closed-UI chat tip when no mentor is in earshot (mentor already speaks the step).
    /// </summary>
    public void OnParticipantProgressChanged(
        EntityUid mob,
        EntityUid guideUid,
        int oldGoalIndex,
        int oldProgressIndex)
    {
        if (!TryComp<TutorialParticipantComponent>(mob, out var part))
            return;

        if (guideUid != EntityUid.Invalid &&
            !TerminatingOrDeleted(guideUid) &&
            TryComp<TutorialGuideComponent>(guideUid, out var guide))
        {
            SnapViewToProgress((guideUid, guide), mob);

            if (_ui.IsUiOpen(guideUid, TutorialPromptUiKey.Key))
            {
                UpdateUi((guideUid, guide), mob);
                return;
            }
        }

        // Mentor in earshot: mentor speaks — skip grey "Next:" toast.
        if (_tutorial.IsMentorCoachingInRange(mob))
            return;

        // Held guide tablet will speak this tip — don't also spam orange chat.
        if (guideUid != EntityUid.Invalid &&
            !TerminatingOrDeleted(guideUid) &&
            TryGetHolderParticipant(guideUid, out var holder, out _) &&
            holder == mob)
            return;

        if (!TryComp<ActorComponent>(mob, out var actor))
            return;

        if (!_tutorial.TryConsumeProgressPopup(actor.PlayerSession))
            return;

        var toast = Loc.GetString("tutorial-server-progress-toast", ("text", part.StepText));
        _tutorial.SendTipChat(actor.PlayerSession, toast);
    }

    private void SnapViewToProgress(Entity<TutorialGuideComponent> ent, EntityUid user)
    {
        if (!TryComp<TutorialParticipantComponent>(user, out var part))
            return;

        if (part.GoalCount > 0)
        {
            ent.Comp.ViewGoalIndex = part.GoalIndex;
            ent.Comp.ViewIndex = part.SubGoalIndex;
        }
        else
        {
            ent.Comp.ViewGoalIndex = 0;
            ent.Comp.ViewIndex = part.StepIndex;
        }
    }

    private void UpdateUi(Entity<TutorialGuideComponent> ent, EntityUid user)
    {
        _ui.SetUiState(ent.Owner, TutorialPromptUiKey.Key, BuildState(ent, user));
    }

    private TutorialPromptBuiState BuildState(Entity<TutorialGuideComponent> ent, EntityUid user)
    {
        if (!TryComp<TutorialParticipantComponent>(user, out var part))
        {
            return new TutorialPromptBuiState { HasTutorial = false };
        }

        // Participant HUD is the source of truth (includes injected chamber-pad steps).
        SnapViewToProgress(ent, user);

        if (part.GoalCount > 0)
        {
            var liveSub = Math.Clamp(part.SubGoalIndex, 0, Math.Max(part.SubGoalCount - 1, 0));
            var subStates = new List<TutorialHudSubGoalState>(part.SubGoalStates.Count);
            foreach (var entry in part.SubGoalStates)
            {
                subStates.Add(new TutorialHudSubGoalState
                {
                    Text = entry.Text,
                    Completed = entry.Completed,
                });
            }

            return new TutorialPromptBuiState
            {
                HasTutorial = true,
                GoalTitle = part.GoalTitle,
                GoalIndex = part.GoalIndex,
                GoalCount = part.GoalCount,
                ViewGoalIndex = part.GoalIndex,
                ViewIndex = liveSub,
                ProgressIndex = liveSub,
                StepCount = Math.Max(part.SubGoalCount, 1),
                StepText = part.StepText,
                ViewComplete = part.StepComplete,
                SubGoalStates = subStates,
                HintText = part.HintText,
                StuckHintText = part.StuckHintText,
                CanGoBack = false,
                WaitingOnSensor = part.StepComplete != TutorialStepComplete.Acknowledge,
                CanGoNext = part.StepComplete == TutorialStepComplete.Acknowledge,
            };
        }

        var progress = part.StepIndex;
        var legacyCount = Math.Max(part.StepCount, 1);

        return new TutorialPromptBuiState
        {
            HasTutorial = true,
            GoalTitle = string.Empty,
            GoalIndex = 0,
            GoalCount = 0,
            ViewGoalIndex = 0,
            ViewIndex = progress,
            ProgressIndex = progress,
            StepCount = legacyCount,
            StepText = part.StepText,
            ViewComplete = part.StepComplete,
            CanGoBack = false,
            WaitingOnSensor = part.StepComplete != TutorialStepComplete.Acknowledge,
            CanGoNext = part.StepComplete == TutorialStepComplete.Acknowledge,
            HintText = part.HintText,
            StuckHintText = part.StuckHintText,
        };
    }

    private void SpeakGuide(
        EntityUid guideUid,
        TutorialGuideComponent guide,
        EntityUid playerUid,
        string subGoalId,
        string dialogue)
    {
        _coach.SpeakAsCoach(guideUid, playerUid, subGoalId, dialogue, id =>
        {
            guide.LastSpokenSubGoal = id;
            Dirty(guideUid, guide);
        });
    }

    private bool TryGetHolderParticipant(
        EntityUid guideUid,
        out EntityUid playerUid,
        out TutorialParticipantComponent part)
    {
        playerUid = default;
        part = default!;

        var xform = Transform(guideUid);

        if (xform.ParentUid == EntityUid.Invalid) return false;

        // Held in a hand container → parent is the mob.
        var holder = xform.ParentUid;
        if (!TryComp(holder, out part!))
        {
            var holderXform = Transform(holder);
            // Nested container (e.g. inventory) — walk up one more level.
            if (!TryComp(holderXform.ParentUid, out part!))
                return false;
            holder = holderXform.ParentUid;
        }

        playerUid = holder;
        return true;
    }
}
