using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Defines HP ranges for animated objects based on their size.
/// This allows per-staff configuration of HP ranges for animated objects.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AnimatedObjectHPComponent : Component
{
    /// <summary>
    /// HP range for items (min, max)
    /// </summary>
    [DataField]
    public Dictionary<string, (int Min, int Max)> Ranges = new() {
        { "Tiny", (15, 25) },
        { "Small", (25, 40) },
        { "Normal", (40, 60) },
        { "Large", (60, 90) },
        { "Huge", (90, 120) },
        { "Ginormous", (120, 150) },
        { "NonItem", (150, 180) },
    };
}