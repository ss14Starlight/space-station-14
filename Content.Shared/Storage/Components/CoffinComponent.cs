using Robust.Shared.GameStates;

namespace Content.Shared.Storage.Components;

/// <summary>
/// Marker component for coffin entities. Used by prototypes (e.g., CrateCoffin).
/// Currently has no behavior; systems may query for this to apply special logic.
/// </summary>
[RegisterComponent]
public sealed partial class CoffinComponent : Component;
