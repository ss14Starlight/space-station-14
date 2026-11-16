namespace Content.Shared._Starlight.Achievement;

public abstract partial class SharedAchievementSystem : EntitySystem
{
    public abstract void QueueUnlockAchievement(EntityUid uid, string achievementId);
}
