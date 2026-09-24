using Robust.Shared.Prototypes;

namespace Content.Shared.Chat.Prototypes;

public sealed partial class EmotePrototype
{
    /// <summary>
    ///     Sorts emotes by priority, from low to high. If not specified, sorts alphabetically.
    /// </summary>
    [DataField]
    public int Priority = int.MaxValue;
}
