using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.FireDragon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FireDragonDashComponent : Component
{
    /// <summary>
    /// World-space unit direction of the dash.
    /// </summary>
    [DataField, AutoNetworkedField] public Vector2 Direction;

    /// <summary>
    /// Dash speed in meters per second.
    /// </summary>
    [DataField, AutoNetworkedField] public float Speed;

    /// <summary>
    /// Time when the dash ends.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan EndTime;
}
