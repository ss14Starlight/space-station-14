using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Stores objective-picker configuration, completion tracking, and server-side scheduling state.
/// </summary>
[RegisterComponent]
public sealed partial class ObjectivePickerConfigurationComponent : Component
{
    /// <summary>
    /// The game rule that created this picker, used to associate the mind with that rule.
    /// </summary>
    public EntityUid? Rule;

    /// <summary>
    /// Normal objective prototype IDs and their relative random-selection weights.
    /// </summary>
    public Dictionary<string, float> Weights = new();

    /// <summary>
    /// Story objective prototype IDs and their relative random-selection weights.
    /// Story objectives are offered separately and have zero difficulty.
    /// They don't count for additional objective unlock requirements.
    /// </summary>
    public Dictionary<string, float> StoryWeights = new();

    /// <summary>
    /// Preferred number of available positive-difficulty offers.
    /// Generation can add more to satisfy the pool's difficulty and compatibility requirements.
    /// </summary>
    public int PreferredOptions;

    /// <summary>
    /// Offered objectives whose targets are chosen only when the selection is confirmed.
    /// Aka, stuff like lesson, kill, help.
    /// </summary>
    public HashSet<NetEntity> DeferredTargets = new();

    /// <summary>
    /// Maps anonymous target tokens used by the client to actual target mind entities.
    /// The mind identities remain server-side while the player chooses objectives.
    /// </summary>
    public Dictionary<int, EntityUid> TargetTokens = new();

    /// <summary>
    /// Whether the current picker has been confirmed or automatically selected.
    /// The configuration remains afterward to track progress and allow additional batches.
    /// </summary>
    public bool Finished;

    /// <summary>
    /// Normal minimum combined difficulty required for a selection, after difficulty modifiers.
    /// Bypassed when given random objectives if there is no possible combination of valid
    /// objectives that fits the minimum.
    /// </summary>
    public float MinimumDifficulty;

    /// <summary>
    /// How long the player has to choose before objectives are automatically assigned.
    /// </summary>
    public TimeSpan SelectionDelay;

    /// <summary>
    /// Whether this mind has spent its mulligan. Carries over to additional picks.
    /// </summary>
    public bool MulliganUsed;

    /// <summary>
    /// Objectives from the latest confirmed batch that count toward unlocking another batch.
    /// </summary>
    public HashSet<EntityUid> CurrentBatch = new();

    /// <summary>
    /// Objectives already observed as completed while tracking confirmed batches.
    /// Done this way so you can't "uncomplete" and "recomplete" the same objective.
    /// </summary>
    public HashSet<EntityUid> CompletedObjectives = new();

    /// <summary>
    /// Whether the latest confirmed batch has met its completion requirement to unlock more.
    /// </summary>
    public bool BatchUnlocked;

    /// <summary>
    /// The player session currently reporting this picker's window as open.
    /// Used for event handling related to the picker's window state to prevent lag.
    /// </summary>
    public ICommonSession? PickerSession;

    /// <summary>
    /// Time when the next periodic open-window refresh becomes due.
    /// Eligibility changes can queue an earlier refresh.
    /// </summary>
    public TimeSpan NextPickerRefresh;

    /// <summary>
    /// Time when this mind's next background objective-progress check becomes due.
    /// </summary>
    public TimeSpan NextProgressCheck;

    /// <summary>
    /// Whether a picker refresh is already queued, preventing duplicate pending jobs.
    /// </summary>
    public bool RefreshQueued;

    /// <summary>
    /// Whether a progress check is already queued, preventing duplicate pending jobs.
    /// </summary>
    public bool ProgressQueued;

    /// <summary>
    /// Eligibility revision used by the last automatic-selection attempt.
    /// Prevents retrying an unchanged failed deadline every tick; -1 allows the first attempt.
    /// </summary>
    public long LastTimeoutRevision = -1;

    /// <summary>
    /// Eligibility revision used when the current or most recent refill started.
    /// A different revision allows depleted-pool generation to be retried.
    /// </summary>
    public long LastRefillRevision = -1;

    /// <summary>
    /// Number of other minds holding each objective prototype when the refill started.
    /// Detects assignment-count changes that may affect objective limits.
    /// </summary>
    public Dictionary<string, int>? RefillCounts;

    /// <summary>
    /// IDs of the offers used to calculate the current objective-conflict rules.
    /// Recalculate those rules when an offer is added, removed, or replaced.
    /// </summary>
    public HashSet<NetEntity> CachedOffers = new();

    /// <summary>
    /// Saved generation work that can continue across later scheduled passes.
    /// Null when no refill is in progress.
    /// </summary>
    internal ObjectivePickerRefillState? Refill;
}

/// <summary>
/// Preserves an objective-generation pass so its remaining work can continue across ticks.
/// </summary>
internal sealed class ObjectivePickerRefillState
{
    /// <summary>
    /// Normal objective prototype IDs and weights still waiting to be tried.
    /// </summary>
    public Dictionary<string, float> Candidates = new();

    /// <summary>
    /// Non-unique objective prototypes that may re-enter the candidate pool
    /// after the current Candidates dictionary is exhausted.
    /// </summary>
    public Dictionary<string, float> Repeatable = new();

    /// <summary>
    /// Story objective prototype IDs and weights still waiting to be tried.
    /// </summary>
    public Dictionary<string, float> Stories = new();

    /// <summary>
    /// Remaining normal-objective attempts across the entire refill.
    /// Each scheduled pass can spend only part of this total.
    /// </summary>
    public int Attempts;

    /// <summary>
    /// Whether normal-objective generation has finished or exhausted its attempts/candidates.
    /// Story-objective generation may still need to run afterward.
    /// </summary>
    public bool CombatFinished;
}
