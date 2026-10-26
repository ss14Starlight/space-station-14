namespace Content.Shared._Starlight.Power.CombustionTurbine;

[RegisterComponent]
public sealed partial class CombustionTurbineStageComponent : Component
{
    [DataField(required: true)]
    public CombustionTurbineStage Stage;

    /// <summary>
    /// Item slot that holds the turbine part.
    /// </summary>
    [DataField]
    public string PartSlot = "turbine_part";

    /// <summary>
    /// The core rotor this section is assembled with. The core rotor points to itself.
    /// </summary>
    [ViewVariables]
    public EntityUid? Rotor;

    [DataField]
    public float PartPryDelay = 2f;
}
