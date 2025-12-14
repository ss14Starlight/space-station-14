using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Component for wizard battle scarves used in recruitment.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WizardBattleScarfComponent : Component
{
    /// <summary>
    /// The faction this scarf belongs to.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Faction = "Red";
}