using System.Threading.Tasks;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Achievement;

public abstract partial class SharedAchievementSystem : EntitySystem
{
    public abstract void QueueUnlockAchievement(EntityUid uid, string achievementId);
    public abstract ValueTask<bool> HasAchievementUnlockedAsync(ICommonSession session, string achievementId);
}
