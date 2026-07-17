using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.Shuttles.Components;

/// <summary>
/// Wallmount terminal that proxies a linked station anchor's PowerCharge UI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAnchorTerminalComponent : Component
{
    /// <summary>
    /// The station anchor this terminal is linked to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedAnchor;

    /// <summary>
    /// Device-link source port used to connect to a station anchor.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> LinkingPort = "StationAnchorTerminalSender";

    /// <summary>
    /// Title shown on the proxied PowerCharge window.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId WindowTitle = "station-anchor-window-title";

    /// <summary>
    /// Accumulator used to flash status lights while the linked anchor is charging or discharging.
    /// </summary>
    [ViewVariables]
    public float FlashAccumulator;

    /// <summary>
    /// Current flash phase for charging/discharging lights.
    /// </summary>
    [ViewVariables]
    public bool FlashLit;
}
