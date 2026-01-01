using Robust.Shared.GameStates;
using Content.Shared.Starlight.Utility;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Hitscan entities that have this component will cause an EMP pulse when striking a target.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanEmpEffectComponent : Component
{
    /// <summary>
    /// The properties of the emp release when striking a target
    /// </summary>
    [DataField]
    public EmpProperties Emp;
}
