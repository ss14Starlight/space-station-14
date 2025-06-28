using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Clothing.Components;

/// <summary>
/// Component that provides immunity to chemical injections when wearing hardsuits.
/// Prevents tranquilizers and other injection-based attacks from affecting the wearer.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HardsuitChemicalImmunityComponent : Component
{
    /// <summary>
    /// Whether the immunity is currently active.
    /// </summary>
    [DataField]
    public bool Active = true;
}
