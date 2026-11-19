namespace Content.Shared._Starlight.Achievement;

public static class AchievementProgressKeys
{
    public const string SpawnCount = "spawn.count";
    public const string SpawnLateJoinCount = "spawn.latejoin.count";
    public const string SpawnRoundStartCount = "spawn.roundstart.count";
    public const string VampireBloodDrank = "vampire.blooddrank";
    public const string AlcoholDrank = "drink.alcohol";
    public const string LizardPlushieArmy = "animate.lizard";
    public const string ScreamCounter = "scream.counter";

    public static string SpawnJob(string jobId) => $"spawn.job.{jobId}";
    public static string StorePurchase(string listingId) => $"store.purchase.{listingId}";
}
