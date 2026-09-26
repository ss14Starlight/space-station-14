using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons.Borgs;

public sealed partial class StationAIShuntableComponent
{
    /// <summary>
    /// The last shunt target used by this AI, if it still exists.
    /// </summary>
    [ViewVariables]
    [DataField, AutoNetworkedField]
    public EntityUid? LastShunt { get; set; }
}
