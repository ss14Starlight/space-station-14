using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CosmicCult.Components.Examine;

/// <summary>
/// Marker component for The Unknown. We also use this to detect its spawn through CultRule!
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CosmicGodComponent : Component
{
    /// <summary>
    /// Triggers round end on spawn if CosmicCult gamerule is active.
    /// The monument spawns a spawner entity that spawns the cosmic god, that has an override
    /// to set this variable to true. Point is, cosmic god is now safe to spawn in a coscult round.
    /// </summary>
    [DataField] public bool TriggerRoundEnd;
}
