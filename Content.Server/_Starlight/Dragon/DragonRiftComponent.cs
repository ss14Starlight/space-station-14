// ReSharper disable CheckNamespace

namespace Content.Server.Dragon;

public sealed partial class DragonComponent
{
    /// <summary>
    /// NPC count for how many Sharkminnows this dragon has.
    /// </summary>
    public HashSet<EntityUid> SharkMinnows = new();
}
