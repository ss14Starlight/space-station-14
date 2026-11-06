using Content.Shared.Chemistry.Components;
using Content.Shared.Fluids.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Fluids.DrainingStorage;

/// <summary>
/// Empties liquids from containers placed inside to a draining buffer, and spills onto the floor if the buffer is full.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(DrainingStorageSystem))]
public sealed partial class DrainingStorageComponent : Component
{
    public const string SolutionName = "buffer";

    /// <summary>
    /// The solution that the buffer is using.
    /// </summary>
    [ViewVariables]
    public Entity<SolutionComponent>? Solution = null;

    /// <summary>
    /// Next time the buffer should perform its draining.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// How fast the buffer drains.
    /// </summary>
    [DataField]
    public float UnitsDrainedPerSecond = 2f;

    /// <summary>
    /// How often the system drains the buffer.
    /// </summary>
    [DataField]
    public TimeSpan DrainInterval = TimeSpan.FromSeconds(1);
}
