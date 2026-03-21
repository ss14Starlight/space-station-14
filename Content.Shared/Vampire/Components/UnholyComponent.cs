using Robust.Shared.GameStates;

namespace Content.Shared.Vampire.Components;

/// <summary>
/// Marker component for entities considered "unholy" by holy items like the Bible.
/// Vampires are marked unholy when their VampireComponent is present.
/// </summary>
[RegisterComponent]
public sealed partial class UnholyComponent : Component
{
}
