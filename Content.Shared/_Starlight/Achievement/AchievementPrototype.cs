using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Achievement;

[Prototype]
public sealed partial class AchievementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Description { get; private set; } = default!;

    [DataField]
    public string Category { get; private set; } = "general";

    [DataField]
    public bool Hidden { get; private set; }

    [DataField]
    public bool ShowProgress { get; private set; } = true;

    [DataField]
    public SpriteSpecifier? Icon { get; private set; }

    [DataField]
    public List<AchievementRequirement> Requirements { get; private set; } = [];

    /// <summary>
    /// Achievements that must already be unlocked before this achievement can be awarded.
    /// </summary>
    [DataField]
    public List<ProtoId<AchievementPrototype>> RequiredAchievements { get; private set; } = [];

    /// <summary>
    /// Optional single-round completed difficulty requirement for an antagonist mind role.
    /// </summary>
    [DataField]
    public DifficultyAntagAchievementRequirement? DifficultyAntag { get; private set; }

    [DataField]
    public List<AchievementReward> Rewards { get; private set; } = [];

    // few helpers that idk where else to put, maybe they should be in a system,
    // but they rely on the prototype data so here we are
    public bool IsRelevantForProgress(string progressType)
        => Requirements.Any(requirement => requirement.ProgressType == progressType);

    public bool AreRequirementsMet(Func<string, bool, double> progressResolver)
        => Requirements.Count > 0
           && Requirements.All(r => progressResolver(r.ProgressType, r.PerRound) >= r.RequiredProgress);
}

/// <summary>
/// Awards an achievement for completed positive-difficulty objectives in one round.
/// Role descendants also qualify, allowing sleeper/reinforcement variants to use the same rule.
/// </summary>
[DataDefinition]
public sealed partial class DifficultyAntagAchievementRequirement
{
    [DataField(required: true)]
    public EntProtoId Role { get; private set; }

    [DataField(required: true)]
    public float RequiredDifficulty { get; private set; }
}

[DataDefinition]
public sealed partial class AchievementReward
{
    [DataField("type", required: true)]
    public AchievementRewardType Type { get; private set; }

    [DataField("id", required: true)]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public LocId? Name { get; private set; }

    [DataField]
    public string? Text { get; private set; }
}

[DataDefinition]
public sealed partial class AchievementRequirement
{
    [DataField(required: true)]
    public string ProgressType { get; private set; } = string.Empty;

    [DataField(required: true)]
    public double RequiredProgress { get; private set; }

    [DataField]
    public bool PerRound { get; private set; }
}
