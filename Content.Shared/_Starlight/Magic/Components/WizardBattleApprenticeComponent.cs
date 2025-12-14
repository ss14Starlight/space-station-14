using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Magic.Components;

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
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Faction = "Red";

    /// <summary>
    /// The spell this apprentice has.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Spell = "Fireball"; // Placeholder

    /// <summary>
    /// The archmage this apprentice serves.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Archmage;
}