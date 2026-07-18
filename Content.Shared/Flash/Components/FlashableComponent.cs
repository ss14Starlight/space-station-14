using Robust.Shared.GameStates;

namespace Content.Shared.Flash.Components;

/// <summary>
/// Marks an entity as eligible for the flashed status effect.
/// Added to prototypes that previously allowed the legacy <c>Flashed</c> status effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlashableComponent : Component;
