using Content.Shared.Atmos;

namespace Content.Shared._Starlight.Atmos;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class IntermittentGasEmitterComponent : Component
{
    /// <summary>
    /// max pressure (kPa), after which no gas will be emitted
    /// </summary>
    [DataField]
    public float MaxPressure = 150;

    /// <summary>
    /// moles per emit
    /// </summary>
    [DataField]
    public float Moles = 30;

    /// <summary>
    /// What gas is produced?
    /// </summary>
    [DataField(required: true)]
    public Gas GasType;

    /// <summary>
    /// how long between gas emissions?
    /// </summary>
    [DataField(required: true)]
    public TimeSpan EmitPeriod;

    [AutoNetworkedField]
    public TimeSpan LastEmit = TimeSpan.FromSeconds(0);
}
