using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityTable;

/// <summary>
/// Context for a table roll that is used to determine if a rule can be added to the table.
/// </summary>
public sealed class GameRuleTableContext
{
    /// <summary>
    /// Gamerules which were already added earlier this round.
    /// </summary>
    public IReadOnlyList<EntProtoId> PreviousRules { get; }

    /// <summary>
    /// Gamerules selected by the current table roll. These are collected before any of them are activated.
    /// This exits to prevent adding a rule to the table that would cause a later rule to not have enough cost/
    /// be incompatible.
    /// </summary>
    public List<EntProtoId> SelectedRules { get; } = new();

    /// <summary>
    /// Gamerules which cannot be selected during the current Dynamic round due to a cooldown from a previous round.
    /// </summary>
    public IReadOnlySet<EntProtoId> Cooldowns { get; }

    public GameRuleTableContext(IReadOnlyList<EntProtoId> previousRules, IReadOnlySet<EntProtoId> cooldowns)
    {
        PreviousRules = previousRules;
        Cooldowns = cooldowns;
    }

    public int Count(EntProtoId rule) =>
        PreviousRules.Count(previous => previous == rule) + SelectedRules.Count(selected => selected == rule);

    public bool Contains(EntProtoId rule) => PreviousRules.Contains(rule) || SelectedRules.Contains(rule);
}
