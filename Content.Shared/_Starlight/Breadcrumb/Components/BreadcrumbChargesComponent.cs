using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Breadcrumb.Components;

/// <summary>
/// Marks an entity as having a breadcrumb charges value. If you use this item on the breadcrumb bag, it recharges it
/// by the amount of charges specified in this component.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BreadcrumbChargesComponent : Component
{
    /// <summary>
    /// The amount of charges this entity is worth.
    /// </summary>
    [DataField] public int Charges = 1;
}
