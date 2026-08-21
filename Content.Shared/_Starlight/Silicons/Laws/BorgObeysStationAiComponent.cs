using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Silicons.Laws;

/// <summary>
/// Gives a silicon a law telling it to obey the station AI, on top of whatever lawset it is running.
/// The law is intrinsic to the chassis rather than part of a lawset, so uploading a different lawboard
/// does not take it away. Emagging (including the FreeMAG) subvert the silicon, which drops it.
/// Ion Storms do not inherintly subvert the silicon, but they do have a chance to overwrite the law.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BorgObeysStationAiComponent : Component
{
    /// <summary>
    /// The law to add at the top of the lawset.
    /// </summary>
    [DataField]
    public ProtoId<SiliconLawPrototype> Law = "BorgObeyStationAi";
}
