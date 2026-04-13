using System;
using System.Collections.Generic;
using System.Text;
using Content.Shared._Starlight.Achievement;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Roles;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class AchievementRewardRequirement : JobRequirement
{
    [DataField]
    public AchievementRewardType RewardType { get; private set; } = AchievementRewardType.Loadout;

    [DataField(required: true)]
    public string RewardId { get; private set; } = string.Empty;

    public override bool Check(
        IEntityManager _,
        ICommonSession? player,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? __,
        IReadOnlyDictionary<string, TimeSpan>? ___,
        out FormattedMessage reason)
    {
        var rewards = IoCManager.Resolve<IAchievementRewardManager>();
        var success = player is not null && rewards.HasReward(player, RewardType, RewardId);
        var sourceAchievements = rewards.GetSourceAchievements(RewardType, RewardId);

        if (TryGetAchievementDisplayText(protoManager, sourceAchievements, success, out var achievementText))
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                success ? "role-achievement-reward-pass" : "role-achievement-reward-fail",
                ("achievement", achievementText)));
        }
        else
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-achievement-reward-fail-hidden"));
        }

        return success;
    }

    private static string? TryGetAchievementName(IPrototypeManager protoManager, string achievementId, bool includeHidden)
    {
        if (!protoManager.TryIndex<AchievementPrototype>(achievementId, out var achievement))
            return achievementId;

        if (!includeHidden && achievement.Hidden)
            return null;

        return Loc.GetString(achievement.Name);
    }

    private bool TryGetAchievementDisplayText(IPrototypeManager protoManager, IReadOnlyList<string> sourceAchievements, bool includeHidden,
        out string achievementText)
    {
        if (sourceAchievements.Count == 0)
        {
            achievementText = RewardId;
            return true;
        }

        var builder = new StringBuilder();
        foreach (var achievementId in sourceAchievements)
        {
            var name = TryGetAchievementName(protoManager, achievementId, includeHidden);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append(name);
        }

        achievementText = builder.ToString();
        return builder.Length > 0;
    }
}
