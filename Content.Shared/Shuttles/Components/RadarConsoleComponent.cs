using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRadarConsoleSystem))]
[AutoGenerateComponentPause] // Starlight
public sealed partial class RadarConsoleComponent : Component
{
    /// <summary>
    /// Maximum radar range. Prefer <see cref="SharedRadarConsoleSystem.SetRange"/> when mutating at runtime.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxRange = 256f;

    /// <summary>
    /// If true, the radar will be centered on the entity. If not - on the grid on which it is located.
    /// </summary>
    [DataField]
    public bool FollowEntity = false;

    #region Starlight
    /// <summary>
    /// When the last interface update was transmitted.
    /// </summary>
    [AutoPausedField]
    public TimeSpan LastInterfaceUpdateTime;
    #endregion
}
