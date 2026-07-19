using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Marks a station entity as having the Sol virology feature enabled.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VirologyStationComponent : Component
{
    /// <summary>
    /// Optional allowlist of pathogen prototype IDs. Empty means all RequiresVirologyStation pathogens.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<PathogenPrototype>> EnabledPathogens = new();

    [DataField, AutoNetworkedField]
    public bool AllowAirborne = true;

    [DataField, AutoNetworkedField]
    public bool AllowFoodborne = true;

    [DataField, AutoNetworkedField]
    public bool AllowSurgeryInfection = true;

    [DataField, AutoNetworkedField]
    public float EnvironmentalPersistenceMultiplier = 1f;
}
