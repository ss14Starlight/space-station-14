using Robust.Shared.GameStates;

namespace Content.Shared.WizardBattle;

/// <summary>
/// Component for wizard battle scarves used in recruitment.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WizardBattleScarfComponent : Component
{
    /// <summary>
    /// The faction this scarf belongs to.
    /// </summary>
    [DataField]
    public string Faction = "Red";
}