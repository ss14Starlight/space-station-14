// _STARLIGHT: Welder Healing Component
// Allows welders to repair silicon-based entities (IPCs, borgs)
// Place this component on the TARGET entity that should be healable with welders

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons;

/// <summary>
/// Allows welders to repair silicon-based entities (IPCs, borgs)
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WelderHealingComponent : Component
{
    /// <summary>
    /// Damage to heal per use. Use negative values to heal.
    /// </summary>
    [DataField]
    public DamageSpecifier DamageHealed = new()
    {
        DamageDict = new() { { "Blunt", -10f } }
    };

    /// <summary>
    /// How long the welding takes
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1.3f);

    /// <summary>
    /// How much fuel to consume per heal
    /// </summary>
    [DataField]
    public float FuelCost = 5f;

    /// <summary>
    /// Which damage container IDs this can heal. If null or empty, heals any.
    /// </summary>
    [DataField]
    public HashSet<string>? AllowedContainers = new() { "Silicon" };
}
