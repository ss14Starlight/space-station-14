using System.Threading.Tasks;
using Content.Shared._Starlight.Achievement;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Achievement;

public sealed class ClientAchievementSystem : SharedAchievementSystem
{
    public override void QueueUnlockAchievement(EntityUid uid, string achievementId)
    {
    }

    public override ValueTask<bool> HasAchievementUnlockedAsync(ICommonSession session, string achievementId)
        => ValueTask.FromResult(false);
}
