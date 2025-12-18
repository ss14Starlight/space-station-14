using Robust.Shared.GameStates;

namespace Content.Shared.WizardBattle;

/// <summary>
/// Component for Wizard Battle Archmage.
/// Tracks recruits, faction, and scaling.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WizardBattleArchmageComponent : Component
{
    /// <summary>
    /// The faction this archmage belongs to (e.g., "Red", "Blue").
    /// </summary>
    [DataField]
    public string Faction = "Red";

    /// <summary>
    /// List of recruited apprentices.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Recruits = new();

    /// <summary>
    /// Number of recruits needed for next ritual word.
    /// </summary>
    [DataField]
    public int NextWordThreshold = 4;

    /// <summary>
    /// Current ritual words collected.
    /// </summary>
    [DataField]
    public List<string> RitualWords = new();

    /// <summary>
    /// The recruitment word for this archmage.
    /// </summary>
    [DataField]
    public string RecruitmentWord = "";
}