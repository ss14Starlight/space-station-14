using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Implants.Components;

/// <summary>
/// Can bypass <see cref="ThickScalesComponent"/> and speeds up injection into the implant owner.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InjectionPortComponent : Component
{
    /// <summary>
    /// If it bypassess <see cref="ThickScalesComponent"/> to allow hypo injections
    /// </summary>
    [DataField]
    public bool BypassScales = true;

    /// <summary>
    /// How much it speeds injections up by
    /// </summary>
    [DataField]
    public float InjectionSpeedMultiplier = 0.5f;
}
