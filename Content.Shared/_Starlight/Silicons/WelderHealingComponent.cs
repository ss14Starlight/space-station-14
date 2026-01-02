// _STARLIGHT: Welder Healing Component
// Allows welders to repair silicon-based entities (IPCs, borgs)
// Place this component on the TARGET entity that should be healable with welders

using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons;

/// <summary>
/// Allows welders to repair silicon-based entities (IPCs, borgs)
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WelderHealingComponent : Component
{
    /// <summary>
    /// How much damage to heal per use
    /// </summary>
    [DataField]
    public float HealAmount = 10f;

    /// <summary>
    /// How long the welding takes
    /// </summary>
    [DataField]
    public float Delay = 1.3f;

    /// <summary>
    /// How much fuel to consume per heal
    /// </summary>
    [DataField]
    public float FuelCost = 5f;
}
