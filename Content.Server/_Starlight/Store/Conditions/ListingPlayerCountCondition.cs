using Content.Shared.Store;
using Robust.Shared.Player;

namespace Content.Server.Store.Conditions;

/// <summary>
/// Only allows a listing to be purchased if population is at a certain number.
/// </summary>
/// <remarks>
/// Basically copied from PlayerCountConidition.
/// </remarks>
public sealed partial class ListingPlayerCountCondition : ListingCondition
{
    /// <summary>
    /// Minimum players needed for this listing to be available.
    /// </summary>
    [DataField]
    public int Minimum = 0;

    /// <summary>
    /// Maximum players allowed for this listing to be available.
    /// </summary>
    [DataField]
    public int Maximum = 500;

    private static ISharedPlayerManager? s_playerManager;

    public override bool Condition(ListingConditionArgs args)
    {
        s_playerManager ??= IoCManager.Resolve<ISharedPlayerManager>();

        var playerCount = s_playerManager.PlayerCount;

        return playerCount >= Minimum && playerCount <= Maximum;
    }
}
