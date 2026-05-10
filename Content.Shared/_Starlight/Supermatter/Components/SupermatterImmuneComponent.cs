using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Supermatter.Components;

/// <summary>
/// Component that provides immunity to supermatter ashing effects.
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class SupermatterImmuneComponent : Component
{
    /// <summary>
    /// When true, protects the entity wearing this item and itself.
    /// When false, only protects the item itself.
    /// </summary>
    [DataField]
    public bool Worn = true;
}
