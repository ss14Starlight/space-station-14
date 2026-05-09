using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Shuttles.Components;

/// <summary>
/// Marks a grid as a syndicate drop pod.
/// Only grids with this component can be targeted and launched by <see cref="DropPodConsoleComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DropPodComponent : Component
{
    /// <summary>
    /// Whether this drop pod has already been launched and is unavailable for further launches.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Launched = false;

    /// <summary>
    /// The station grid this pod will merge into when it lands.
    /// Set by DropPodConsoleSystem at launch time (server-only, not networked).
    /// </summary>
    public EntityUid? TargetStationGrid;
}
