using Content.Shared._Starlight.Spawners.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Spawners.Components;

/// <summary>
/// When a <c>TimedDespawnComponent</c> despawns, another one will be spawned in its place.
/// </summary>
[RegisterComponent, Access(typeof(SharedSpawnOnDespawnSystem))]
public sealed partial class SpawnOnDespawnComponent : Component
{
    /// <summary>
    /// Entity prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype = string.Empty;

    #region Starlight

    /// <summary>
    /// Component overrides for the spawned entity.
    /// </summary>
    [DataField] public ComponentRegistry? Overrides;

    #endregion
}
