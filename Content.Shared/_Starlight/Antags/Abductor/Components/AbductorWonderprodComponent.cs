using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Antags.Abductor.Components;

/// <summary>
/// Component for abductor wonderprods that allows them to have reduced effectiveness
/// against hardsuit-protected targets instead of being completely blocked.
/// </summary>
[RegisterComponent]
public sealed partial class AbductorWonderprodComponent : Component
{
    /// <summary>
    /// Fallback stamina damage to use when hitting hardsuit-protected targets.
    /// Should almost match stunbaton damage for balanced gameplay.
    /// </summary>
    [DataField]
    public float FallbackStaminaDamage;
}
