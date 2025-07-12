using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Mindshield.Components;

/// <summary>
/// Component that marks an entity as having had their mindshield destroyed.
/// Prevents re-implantation of mindshields.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DestroyedMindshieldComponent : Component
{
    /// <summary>
    /// When the mindshield was destroyed
    /// </summary>
    [DataField("destroyedAt")]
    public TimeSpan DestroyedAt;
}
