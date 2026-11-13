using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Reptilian.Components;

/// <summary>
/// Scales too thick for quick needles. Hyposprays, jet injectors, darts and injecting melee weapons
/// can't get through, and syringes take much longer to push in. Drawing blood is unaffected.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThickScalesComponent : Component
{
    /// <summary>
    /// Multiplier for the do-after of syringe-like injectors (ones that are not used in hand) on this mob.
    /// </summary>
    [DataField]
    public float InjectTimeMultiplier = 2f;

    /// <summary>
    /// Whether instant injectors (hyposprays, hypopens, jet injectors) are blocked entirely.
    /// </summary>
    [DataField]
    public bool BlockInstantInjectors = true;

    /// <summary>
    /// Whether darts, injecting projectiles and injecting melee weapons are blocked entirely.
    /// </summary>
    [DataField]
    public bool BlockRemoteInjection = true;
}
