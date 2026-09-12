using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Scripted tutorial coach that speaks lines keyed by the player's current sub-goal id.
/// </summary>
/// <remarks>
/// Multiple <see cref="TutorialTrainerLine"/> entries may share a sub-goal id; they queue and are
/// spoken one at a time. Keep each one to a single sentence — a wall of text in a speech bubble
/// scrolls away before a new player has read it.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class TutorialTrainerComponent : Component
{
    /// <summary>
    /// Dialogue lines spoken when the matching sub-goal is current, in author order.
    /// </summary>
    [DataField]
    public List<TutorialTrainerLine> Lines = new();

    /// <summary>
    /// Beats this coach deliberately has nothing to say on, because somebody else is speaking
    /// them. Without it a coach with no authored line for a sub-goal falls back to reading the
    /// objectives checklist aloud, which would have him talking over the voice the beat was
    /// handed to.
    /// </summary>
    [DataField]
    public List<string> SilentSubGoals = new();

    /// <summary>
    /// Sub-goal id whose lines were last queued (change detection; no timed reminders).
    /// </summary>
    [DataField]
    public string? LastSpokenSubGoal;

    /// <summary>
    /// Lines still waiting to be spoken for <see cref="LastSpokenSubGoal"/>.
    /// </summary>
    [ViewVariables]
    public Queue<TutorialPendingLine> PendingLines = new();

    /// <summary>
    /// Lines of <see cref="LastSpokenSubGoal"/> flagged <see cref="TutorialTrainerLine.AfterComplete"/>,
    /// waiting for the player to finish the beat.
    /// </summary>
    [ViewVariables]
    public Queue<TutorialPendingLine> PendingAfterLines = new();

    /// <summary>
    /// Sub-goal whose reaction is being spoken right now. While this is set the beat is satisfied
    /// but deliberately not advanced, so the coach gets to finish before the next objective lands.
    /// </summary>
    [ViewVariables]
    public string? ReactingFor;

    /// <summary>
    /// When the next queued line may be spoken. Null while waiting on the player to come close.
    /// </summary>
    [ViewVariables]
    public TimeSpan? NextLineAt;

    /// <summary>
    /// Gap still owed to the line spoken before the segment changed, carried across the boundary.
    /// </summary>
    /// <remarks>
    /// Finishing an objective must not fire the next segment's opening line on the same tick. The
    /// pause between lines is the whole illusion: it reads as somebody typing, and it is the time
    /// the player has to take in what was just said. A beat boundary is not a reason to skip it.
    /// </remarks>
    [ViewVariables]
    public TimeSpan? CarriedGap;

    /// <summary>
    /// When the line currently on screen stops being the newest thing this coach said.
    /// </summary>
    /// <remarks>
    /// Survives a change of sub-goal, unlike <see cref="NextLineAt"/>, which is why the walking
    /// coach reads this one: he must not turn and go while a sentence of his is still up, and by
    /// the time the next segment has been queued NextLineAt is about the next thing he will say
    /// rather than the last thing he did.
    /// </remarks>
    [ViewVariables]
    public TimeSpan SpeakingUntil;

    /// <summary>
    /// When the typing indicator may come back on after a line has been sent.
    /// </summary>
    [ViewVariables]
    public TimeSpan? TypingResumeAt;

    /// <summary>
    /// Beat between a line appearing and the indicator starting up again, so it blinks off the way
    /// it does for somebody who just hit enter and is thinking about the next sentence.
    /// </summary>
    [DataField]
    public TimeSpan TypingPause = TimeSpan.FromSeconds(0.7);

    /// <summary>
    /// Lines of <see cref="LastSpokenSubGoal"/> said out loud, reset when the segment changes, so a
    /// staged effect can land on a line of the script rather than on a stopwatch.
    /// </summary>
    [ViewVariables]
    public int LinesSpoken;

    /// <summary>
    /// How close the player must get before the coach starts a queued segment. Null speaks as soon
    /// as the sub-goal becomes current, which is what walking mentors want since they follow you.
    /// A holopad coach uses a small radius so the player walks up to them rather than catching
    /// dialogue through a doorway.
    /// </summary>
    [DataField]
    public float? SpeakRange;

    /// <summary>
    /// Pause after the player comes in range before the first line of a segment. Gives them a beat
    /// to arrive and look at the coach instead of reading mid-stride.
    /// </summary>
    [DataField]
    public TimeSpan StartDelay = TimeSpan.Zero;

    /// <summary>
    /// Extra pause before the very first thing this coach ever says, on top of
    /// <see cref="StartDelay"/>. The player has just been dropped into a body and is still
    /// finding the screen; opening on them instantly reads as a bug.
    /// </summary>
    [DataField]
    public TimeSpan SessionStartDelay = TimeSpan.Zero;

    /// <summary>
    /// True once this coach has spoken at least once, so <see cref="SessionStartDelay"/> only
    /// applies to the opening line.
    /// </summary>
    [ViewVariables]
    public bool HasSpoken;

    /// <summary>
    /// True once the player has come within <see cref="SpeakRange"/> since this coach arrived where
    /// they now are. Cleared when a holopad coach re-projects into a different chamber.
    /// </summary>
    /// <remarks>
    /// The range check is a "have you got here yet" gate, not a leash. Applying it to every segment
    /// meant a drill that deliberately sends the player down a lane silenced her for the rest of the
    /// chamber; once they have walked up to her, she keeps talking however far they wander.
    /// </remarks>
    [ViewVariables]
    public bool PlayerArrived;

    /// <summary>
    /// Holds a queued segment at the gate. A coach who leads sets this while he is walking, so the
    /// next section starts when he gets there rather than being shouted over his shoulder on the
    /// way. Only the start of a segment is gated: once he has begun, a shove does not cut him off.
    /// </summary>
    [ViewVariables]
    public bool SpeechHeld;

    /// <summary>
    /// Rate limit for one-off corrections (see <see cref="TutorialSubGoalData.RetryLine"/>).
    /// </summary>
    [ViewVariables]
    public TimeSpan? NextInterjectionAt;

    /// <summary>
    /// Minimum gap between one-off corrections, so a player who keeps sprinting is told once
    /// rather than every tick.
    /// </summary>
    [DataField]
    public TimeSpan InterjectionCooldown = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Floor on the gap between consecutive lines. Zero keeps the original speak-everything-at-once
    /// behaviour for coaches that author a single line per sub-goal.
    /// </summary>
    [DataField]
    public TimeSpan MinLineDelay = TimeSpan.Zero;

    /// <summary>
    /// Ceiling on the gap between consecutive lines, however long the line is. Default is the
    /// lifetime of a speech bubble (<c>SpeechBubble.TotalTime</c>), so the next line lands as the
    /// last one fades rather than leaving the player staring at an empty screen.
    /// </summary>
    [DataField]
    public TimeSpan MaxLineDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Added per character of the line that is coming next, approximating someone typing it out.
    /// The long pause belongs in front of the long message, not behind it.
    /// </summary>
    [DataField]
    public float SecondsPerCharacter;

    /// <summary>
    /// Unused legacy field kept for map/component compatibility.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextReminderAt;
}

/// <summary>
/// How far through a sub-goal's script a coach is.
/// </summary>
public enum TutorialCoachSpeech : byte
{
    /// <summary>No coach, or she has said everything this beat needs.</summary>
    Done,

    /// <summary>A segment is queued but has not started: nobody has walked into earshot yet.</summary>
    Waiting,

    /// <summary>Mid-script: a line is out, or the gap before the next one is running.</summary>
    Speaking,
}

/// <summary>
/// One trainer speech line tied to a curriculum sub-goal id.
/// </summary>
[DataDefinition]
public sealed partial class TutorialTrainerLine
{
    [DataField(required: true)]
    public string SubGoalId = string.Empty;

    [DataField(required: true)]
    public LocId Dialogue = default!;

    /// <summary>
    /// Release the sub-goal's control hint as this line is spoken, rather than waiting for the
    /// whole segment to finish. For the line that actually asks the player to do the thing.
    /// </summary>
    [DataField]
    public bool ShowControlHint;

    /// <summary>
    /// Hold this line back until the player has actually done the thing, then say it and only then
    /// move on to the next beat.
    /// </summary>
    /// <remarks>
    /// For lines that react rather than instruct: "hear that beep?", "see, it just says Passenger".
    /// Said on the ordinary timer they land while the player is still reading the objective, and
    /// the coach comes off as talking about something that has not happened yet. Anything left
    /// unsaid from the instruction half of the segment is dropped when the reaction starts, since
    /// the player has evidently stopped needing to be told.
    /// </remarks>
    [DataField]
    public bool AfterComplete;
}

/// <summary>
/// One queued line of coach dialogue: resolved text plus whether speaking it should reveal the
/// control hint.
/// </summary>
public readonly record struct TutorialPendingLine(string Text, bool ShowControlHint, bool AfterComplete = false);
