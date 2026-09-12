using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Systems;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Speaks coach lines for mentors (and shared dialogue resolution), handles click-to-repeat /
/// Acknowledge advance / stuck hints when there is no handheld guide.
/// Speaks once per sub-goal change (not on a timer); muted while the guide UI is open.
/// </summary>
public sealed partial class TutorialTrainerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialTrainerComponent, InteractHandEvent>(OnInteractHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var trainers = EntityQueryEnumerator<TutorialTrainerComponent, TransformComponent>();
        while (trainers.MoveNext(out var trainerUid, out var trainer, out var trainerXform))
        {
            if (TryComp<MobStateComponent>(trainerUid, out var mobState) &&
                mobState.CurrentState is MobState.Dead or MobState.Critical)
                continue;

            if (!TryResolvePlayer(trainerUid, trainerXform, out var playerUid, out var part))
                continue;

            if (!TryResolveDialogue(trainerUid, trainer, playerUid, part, out var subGoalId, out var dialogue))
                continue;

            // Only queue when the sub-goal changes — no timed reminders.
            if (trainer.LastSpokenSubGoal != subGoalId)
            {
                trainer.LastSpokenSubGoal = subGoalId;

                // Whatever pause the last line earned is still owed. Dropping it here is what made
                // finishing an objective fire the next segment's opening line on the same tick.
                trainer.CarriedGap = trainer.NextLineAt;
                trainer.PendingLines.Clear();
                trainer.PendingAfterLines.Clear();
                trainer.ReactingFor = null;
                trainer.NextLineAt = null;
                trainer.LinesSpoken = 0;

                // Don't pile IC speech on top of an open tutorial prompt, and don't talk over a
                // beat that was handed to somebody else. The bookkeeping above still runs for a
                // silent beat: callers read a coach with nothing queued as finished, and one that
                // never took the segment at all as about to start.
                if (!_tutorial.IsGuideUiOpen(playerUid) && !IsSilent(trainer, subGoalId))
                {
                    foreach (var line in ResolveSegment(trainer, subGoalId, dialogue))
                    {
                        if (line.AfterComplete)
                            trainer.PendingAfterLines.Enqueue(line);
                        else
                            trainer.PendingLines.Enqueue(line);
                    }
                }

                Dirty(trainerUid, trainer);
            }

            // Reacting means the beat is already satisfied and being held open for him; the
            // reaction queue is the only one that matters until he has finished it.
            var queue = ActiveQueue(trainer);

            if (queue.Count == 0)
            {
                if (trainer.ReactingFor == null)
                    continue;

                // Last word said: release the beat that was waiting on him.
                trainer.ReactingFor = null;
                Dirty(trainerUid, trainer);
                _tutorial.AdvanceSubGoal(playerUid);
                continue;
            }

            // Coaches with a speak range wait for the player to walk up to them once, and then talk
            // for as long as they are stationed there. Re-checking the range every segment silenced
            // her for the rest of a chamber the moment a drill sent the player down a lane; a
            // holopad coach only has to be arrived at, and that is what re-projecting into the next
            // chamber resets.
            if (trainer.NextLineAt == null)
            {
                // A coach still walking to the spot has not got to the next section yet.
                if (trainer.SpeechHeld)
                    continue;

                if (!trainer.PlayerArrived)
                {
                    if (!IsPlayerInSpeakRange(trainer, trainerXform, playerUid))
                        continue;

                    trainer.PlayerArrived = true;
                }

                var opening = trainer.HasSpoken ? trainer.StartDelay : trainer.StartDelay + trainer.SessionStartDelay;
                var start = now + opening;

                // Never sooner than the gap the previous line bought.
                if (trainer.CarriedGap is { } carried && carried > start)
                    start = carried;

                trainer.CarriedGap = null;
                trainer.NextLineAt = start;
                Dirty(trainerUid, trainer);
            }

            if (now < trainer.NextLineAt.Value)
                continue;

            var next = queue.Dequeue();
            trainer.NextLineAt = now + ResolveNextLineDelay(trainer);
            trainer.HasSpoken = true;
            trainer.LinesSpoken++;
            Dirty(trainerUid, trainer);

            SpeakLine(trainerUid, playerUid, subGoalId, next.Text);
            trainer.TypingResumeAt = now + trainer.TypingPause;
            trainer.SpeakingUntil = trainer.NextLineAt.Value;

            if (next.ShowControlHint)
                _tutorial.ShowPendingControlHint(playerUid);
        }

        UpdateTypingIndicators(now);
    }

    /// <summary>
    /// Runs the three dots over a coach's head while the next line is on its way.
    /// </summary>
    /// <remarks>
    /// The pause between lines is meant to read as somebody typing, and without the indicator it
    /// reads as somebody who has stopped. Held off for a moment after each line so it blinks out
    /// the way it does for a person who just hit enter, rather than burning continuously through a
    /// whole segment.
    /// </remarks>
    private void UpdateTypingIndicators(TimeSpan now)
    {
        var query = EntityQueryEnumerator<TutorialTrainerComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var trainer, out var appearance))
        {
            var writing = ActiveQueue(trainer).Count > 0
                && trainer.NextLineAt is { } due
                && now < due
                && (trainer.TypingResumeAt is not { } resume || now >= resume);

            _appearance.SetData(
                uid,
                TypingIndicatorVisuals.State,
                writing ? TypingIndicatorState.Typing : TypingIndicatorState.None,
                appearance);
        }
    }

    /// <summary>The coach bound to this player, if one is coaching them.</summary>
    private bool TryGetCoach(
        EntityUid player,
        out EntityUid coachUid,
        [NotNullWhen(true)] out TutorialTrainerComponent? trainer)
    {
        var coaches = EntityQueryEnumerator<TutorialTrainerComponent, TutorialMentorComponent>();
        while (coaches.MoveNext(out var uid, out var comp, out var mentor))
        {
            if (mentor.PlayerUid != player)
                continue;

            coachUid = uid;
            trainer = comp;
            return true;
        }

        coachUid = EntityUid.Invalid;
        trainer = null;
        return false;
    }

    /// <summary>
    /// Starts the coach's reaction to a beat the player has just satisfied, if he has one owed.
    /// </summary>
    /// <returns>
    /// True when a reaction started, meaning the caller must leave the beat where it is: the coach
    /// advances it himself once he has finished speaking.
    /// </returns>
    /// <remarks>
    /// Anything still queued from the instruction half is dropped. The player has plainly stopped
    /// needing to be told how, and "go on, try it" landing after they already have is the same
    /// complaint this whole mechanism exists to answer.
    /// </remarks>
    public bool TryStartReaction(EntityUid player, string subGoalId)
    {
        if (!TryGetCoach(player, out var coachUid, out var trainer))
            return false;

        // Already mid-reaction to this beat: still true, because the answer this returns is "leave
        // the beat where it is", not "a reaction started just now". Polled sensors keep calling in
        // every tick for as long as their condition holds, and answering false to the second call
        // advanced the beat out from under the reaction the first one had just begun.
        if (trainer.ReactingFor != null)
            return string.Equals(trainer.ReactingFor, subGoalId, StringComparison.Ordinal);

        if (!string.Equals(trainer.LastSpokenSubGoal, subGoalId, StringComparison.Ordinal))
            return false;

        if (trainer.PendingAfterLines.Count == 0)
            return false;

        trainer.PendingLines.Clear();
        trainer.ReactingFor = subGoalId;

        // No fresh arrival pause in front of the reaction, since he is reacting to something that
        // just happened in front of him. The gap already owed to the line before it still stands,
        // though: a player who finishes the beat half a second after he starts a sentence should
        // not get the next one on top of it.
        var now = _timing.CurTime;
        if (trainer.NextLineAt is not { } due || due < now)
            trainer.NextLineAt = now;
        Dirty(coachUid, trainer);
        return true;
    }

    /// <summary>
    /// How many lines of <paramref name="subGoalId"/> this player's coach has spoken. False when no
    /// coach is on that segment, which callers must read as "no cue is coming", not as zero.
    /// </summary>
    public bool TryGetLinesSpoken(EntityUid player, string subGoalId, out int spoken)
    {
        spoken = 0;

        var coaches = EntityQueryEnumerator<TutorialTrainerComponent, TutorialMentorComponent>();
        while (coaches.MoveNext(out _, out var trainer, out var mentor))
        {
            if (mentor.PlayerUid != player)
                continue;

            // A different segment means her count belongs to another beat.
            if (!string.Equals(trainer.LastSpokenSubGoal, subGoalId, StringComparison.Ordinal))
                return false;

            spoken = trainer.LinesSpoken;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Where a coach is in <paramref name="subGoalId"/>'s script. Callers that hold something back
    /// until she is done need <see cref="TutorialCoachSpeech.Waiting"/> separated from
    /// <see cref="TutorialCoachSpeech.Speaking"/>: only the former can go on forever, so only the
    /// former may be timed out.
    /// </summary>
    public TutorialCoachSpeech ResolveSegmentState(EntityUid mentor, string subGoalId)
    {
        if (!TryComp<TutorialTrainerComponent>(mentor, out var trainer))
            return TutorialCoachSpeech.Done;

        // The segment is enqueued by this system's own Update, which may not have run since the
        // sub-goal changed. Until it has, she is about to start rather than finished.
        if (!string.Equals(trainer.LastSpokenSubGoal, subGoalId, StringComparison.Ordinal))
            return TutorialCoachSpeech.Waiting;

        // Lines queued with no clock running means nobody has walked into earshot yet.
        if (trainer.PendingLines.Count > 0 && trainer.NextLineAt == null)
            return TutorialCoachSpeech.Waiting;

        if (trainer.PendingLines.Count > 0 ||
            (trainer.NextLineAt is { } next && _timing.CurTime < next))
        {
            return TutorialCoachSpeech.Speaking;
        }

        return TutorialCoachSpeech.Done;
    }

    /// <summary>
    /// Where every voice on this beat is in its script, loudest state winning.
    /// </summary>
    /// <remarks>
    /// A beat handed to a second coach (the holopad she is projected onto, a bystander) must not be
    /// cut short by the mentor having nothing to say on it, which is what asking the mentor alone
    /// would report. Only coaches with a line authored for this beat are counted, so the ordinary
    /// case is still the mentor and nobody else.
    /// </remarks>
    public TutorialCoachSpeech ResolveBeatSpeech(EntityUid player, EntityUid mentor, string subGoalId)
    {
        var loudest = mentor == EntityUid.Invalid || TerminatingOrDeleted(mentor)
            ? TutorialCoachSpeech.Done
            : ResolveSegmentState(mentor, subGoalId);

        if (loudest == TutorialCoachSpeech.Speaking)
            return loudest;

        var mapUid = Transform(player).MapUid;
        if (mapUid == null)
            return loudest;

        var query = EntityQueryEnumerator<TutorialTrainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trainer, out var xform))
        {
            if (uid == mentor || xform.MapUid != mapUid || !HasLineFor(trainer, subGoalId))
                continue;

            var state = ResolveSegmentState(uid, subGoalId);
            if (state > loudest)
                loudest = state;

            if (loudest == TutorialCoachSpeech.Speaking)
                break;
        }

        return loudest;
    }

    /// <summary>
    /// Holds this coach's next segment at the gate, or lets it go. See
    /// <see cref="TutorialTrainerComponent.SpeechHeld"/>.
    /// </summary>
    public void HoldSpeech(EntityUid trainerUid, bool held)
    {
        if (!TryComp<TutorialTrainerComponent>(trainerUid, out var trainer) || trainer.SpeechHeld == held)
            return;

        trainer.SpeechHeld = held;
        Dirty(trainerUid, trainer);
    }

    /// <summary>
    /// Speaks a one-off correction outside the sub-goal script, rate limited so a player who keeps
    /// breaking a drill is corrected once rather than continuously.
    /// </summary>
    public void TrySpeakInterjection(EntityUid mentor, EntityUid player, LocId line)
    {
        if (!TryComp<TutorialTrainerComponent>(mentor, out var trainer))
            return;

        var now = _timing.CurTime;
        if (trainer.NextInterjectionAt is { } ready && now < ready)
            return;

        var text = Loc.GetString(line);
        if (string.IsNullOrWhiteSpace(text))
            return;

        trainer.NextInterjectionAt = now + trainer.InterjectionCooldown;
        Dirty(mentor, trainer);
        SpeakAsCoach(mentor, player, string.Empty, text, null);
    }

    private void OnInteractHand(Entity<TutorialTrainerComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TutorialParticipantComponent>(args.User, out var part))
            return;

        // Mentors only coach their bound player.
        if (TryComp<TutorialMentorComponent>(ent, out var mentor) &&
            mentor.PlayerUid != EntityUid.Invalid &&
            mentor.PlayerUid != args.User)
            return;

        if (!TryResolveDialogue(ent, ent.Comp, args.User, part, out var subGoalId, out var dialogue))
            return;

        // Mid-segment: clicking pulls the next line forward for players who read faster than the
        // coach talks, rather than repeating what they just heard.
        var pending = ActiveQueue(ent.Comp);
        if (pending.Count > 0)
        {
            args.Handled = true;
            var next = pending.Dequeue();
            ent.Comp.NextLineAt = _timing.CurTime + ResolveNextLineDelay(ent.Comp);
            Dirty(ent.Owner, ent.Comp);
            SpeakLine(ent, args.User, subGoalId, next.Text);

            if (next.ShowControlHint)
                _tutorial.ShowPendingControlHint(args.User);
            return;
        }

        // Nothing left to pull forward, so the click does not speak: repeating the line the player
        // just read is noise, and the chat log already has every word of it a scroll away. The
        // click still does its two useful jobs below.
        if (part.StepComplete == TutorialStepComplete.Acknowledge)
        {
            args.Handled = true;
            _tutorial.AdvanceSubGoal(args.User);
            return;
        }

        // InteractMentor is completed by InteractionPopupSystem → InteractionSuccessEvent
        // (TutorialGoalSensorSystem.OnMentorHugged). Do not mark Handled or the hug never fires
        // and the stuck hint loops forever on every empty-hand click.
        if (part.StepComplete == TutorialStepComplete.InteractMentor)
            return;

        // Waiting on a sensor: click shows the stuck hint when authored.
        args.Handled = true;
        if (!string.IsNullOrEmpty(part.StuckHintText))
            _tutorial.SendTipChat(args.User, part.StuckHintText);
    }

    /// <summary>
    /// Resolves coach dialogue: trainer line override, else live sub-goal text.
    /// </summary>
    public bool TryResolveDialogue(
        EntityUid coachUid,
        TutorialTrainerComponent? trainer,
        EntityUid playerUid,
        TutorialParticipantComponent part,
        out string subGoalId,
        out string dialogue)
    {
        subGoalId = string.Empty;
        dialogue = string.Empty;

        if (_tutorial.TryGetCurrentSubGoal(playerUid, part, out var sub))
        {
            subGoalId = sub.Id;
            if (trainer != null && TryGetOverrideLine(trainer, sub.Id, out var overrideLoc))
            {
                dialogue = Loc.GetString(overrideLoc);
                return !string.IsNullOrWhiteSpace(dialogue);
            }

            dialogue = Loc.GetString(sub.Text);
            return !string.IsNullOrWhiteSpace(dialogue);
        }

        // Legacy flat steps.
        if (part.StepCount <= 0 || string.IsNullOrEmpty(part.StepText))
            return false;

        subGoalId = $"legacy:{part.StepIndex}";
        dialogue = part.StepText;
        return true;
    }

    /// <summary>
    /// Guide / mentor shared speak helper. Always speaks IC (speech bubble).
    /// Keybind markup is stripped for the spoken line; resolved binds stay available via
    /// stuck-hint tip chat / the guide UI, not a duplicate grey progress toast.
    /// </summary>
    public void SpeakAsCoach(
        EntityUid speakerUid,
        EntityUid playerUid,
        string subGoalId,
        string dialogue,
        Action<string>? markSpoken)
    {
        // playerUid reserved for future per-player coach delivery (e.g. whisper range).
        _ = playerUid;

        var spoken = FormattedMessage.RemoveMarkupPermissive(dialogue);
        if (!string.IsNullOrWhiteSpace(spoken))
        {
            _chat.TrySendInGameICMessage(
                speakerUid,
                spoken,
                InGameICChatType.Speak,
                hideChat: false,
                hideLog: true,
                ignoreActionBlocker: true);
        }

        markSpoken?.Invoke(subGoalId);
    }

    private void SpeakLine(
        EntityUid trainerUid,
        EntityUid playerUid,
        string subGoalId,
        string dialogue)
    {
        SpeakAsCoach(trainerUid, playerUid, subGoalId, dialogue, null);
    }

    /// <summary>
    /// All authored lines for a sub-goal in order, or the resolved fallback when none are authored.
    /// </summary>
    private List<TutorialPendingLine> ResolveSegment(
        TutorialTrainerComponent trainer,
        string subGoalId,
        string fallback)
    {
        var lines = new List<TutorialPendingLine>();
        foreach (var line in trainer.Lines)
        {
            if (!string.Equals(line.SubGoalId, subGoalId, StringComparison.Ordinal))
                continue;

            var text = Loc.GetString(line.Dialogue);
            if (!string.IsNullOrWhiteSpace(text))
                lines.Add(new TutorialPendingLine(text, line.ShowControlHint, line.AfterComplete));
        }

        if (lines.Count == 0 && !string.IsNullOrWhiteSpace(fallback))
            lines.Add(new TutorialPendingLine(fallback, false));

        return lines;
    }

    private bool IsPlayerInSpeakRange(
        TutorialTrainerComponent trainer,
        TransformComponent trainerXform,
        EntityUid playerUid)
    {
        if (trainer.SpeakRange is not { } range)
            return true;

        var playerXform = Transform(playerUid);

        if (playerXform.MapID != trainerXform.MapID) return false;

        var delta = _transform.GetWorldPosition(playerXform) - _transform.GetWorldPosition(trainerXform);
        return delta.Length() <= range;
    }

    /// <summary>
    /// Gap before the line at the head of the queue, scaled by how long *that* line is.
    /// </summary>
    /// <remarks>
    /// Scaling the pause to the line just spoken had it backwards: someone typing spends the long
    /// silence composing the long message, not recovering from it. With nothing left to say the gap
    /// collapses to the floor, since it only has to let the closing line be read.
    /// </remarks>
    private static TimeSpan ResolveNextLineDelay(TutorialTrainerComponent trainer)
    {
        // Whichever queue is live. Reading PendingLines while a reaction is running finds it empty
        // and falls through to the floor delay, which drops the typing pause from every reaction
        // line and rattles them out back to back.
        if (!ActiveQueue(trainer).TryPeek(out var upcoming))
            return trainer.MinLineDelay;

        var typed = TimeSpan.FromSeconds(upcoming.Text.Length * trainer.SecondsPerCharacter);
        if (typed < trainer.MinLineDelay)
            typed = trainer.MinLineDelay;

        return typed > trainer.MaxLineDelay ? trainer.MaxLineDelay : typed;
    }

    /// <summary>
    /// The queue this coach is currently speaking from: the reaction to a finished beat if one is
    /// running, the beat's own script otherwise.
    /// </summary>
    private static Queue<TutorialPendingLine> ActiveQueue(TutorialTrainerComponent trainer)
        => trainer.ReactingFor != null ? trainer.PendingAfterLines : trainer.PendingLines;

    /// <summary>
    /// Clears the arrival gate, so this coach waits for the player to walk up to her again.
    /// </summary>
    public void ResetArrival(EntityUid trainerUid)
    {
        if (!TryComp<TutorialTrainerComponent>(trainerUid, out var trainer) || !trainer.PlayerArrived)
            return;

        trainer.PlayerArrived = false;
        Dirty(trainerUid, trainer);
    }

    private bool TryResolvePlayer(
        EntityUid trainerUid,
        TransformComponent trainerXform,
        out EntityUid playerUid,
        out TutorialParticipantComponent part)
    {
        if (TryComp<TutorialMentorComponent>(trainerUid, out var mentor) &&
            mentor.PlayerUid != EntityUid.Invalid &&
            !TerminatingOrDeleted(mentor.PlayerUid) &&
            TryComp(mentor.PlayerUid, out part!))
        {
            playerUid = mentor.PlayerUid;
            return true;
        }

        var mapUid = trainerXform.MapUid;
        if (mapUid == null)
        {
            playerUid = default;
            part = default!;
            return false;
        }

        var participants = EntityQueryEnumerator<TutorialParticipantComponent, TransformComponent>();
        while (participants.MoveNext(out playerUid, out part!, out var playerXform))
        {
            if (playerXform.MapUid == mapUid)
                return true;
        }

        playerUid = default;
        part = default!;
        return false;
    }

    private static bool IsSilent(TutorialTrainerComponent trainer, string subGoalId)
        => trainer.SilentSubGoals.Contains(subGoalId);

    private static bool HasLineFor(TutorialTrainerComponent trainer, string subGoalId)
    {
        foreach (var line in trainer.Lines)
        {
            if (string.Equals(line.SubGoalId, subGoalId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool TryGetOverrideLine(TutorialTrainerComponent trainer, string subGoalId, out LocId dialogue)
    {
        foreach (var line in trainer.Lines)
        {
            if (!string.Equals(line.SubGoalId, subGoalId, StringComparison.Ordinal))
                continue;

            dialogue = line.Dialogue;
            return true;
        }

        dialogue = default;
        return false;
    }
}
