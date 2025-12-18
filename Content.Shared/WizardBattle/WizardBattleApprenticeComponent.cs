using Robust.Shared.GameStates;

namespace Content.Shared.WizardBattle;

/// <summary>
/// Component for Wizard Battle Apprentice.
/// Tracks their spell and faction.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WizardBattleApprenticeComponent : Component
{
    /// <summary>
    /// The faction this apprentice belongs to.
    /// </summary>
    [DataField]
    public string Faction = "Red";

    /// <summary>
    /// The archmage this apprentice serves.
    /// </summary>
    [DataField]
    public EntityUid? Archmage;

    /// <summary>
    /// The spell assigned to this apprentice.
    /// </summary>
    [DataField]
    public string Spell = "";
}