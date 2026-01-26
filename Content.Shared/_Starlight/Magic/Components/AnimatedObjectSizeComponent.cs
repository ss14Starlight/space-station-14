using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Stores the original item size of an animated object before ItemComponent is removed.
/// Used by server to determine appropriate HP values.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AnimatedObjectSizeComponent : Component
{
    /// <summary>
    /// The original item size ID (Tiny, Small, Normal, Large, Huge, Ginormous)
    /// </summary>
    [DataField]
    public string OriginalSize = "Normal";
}
