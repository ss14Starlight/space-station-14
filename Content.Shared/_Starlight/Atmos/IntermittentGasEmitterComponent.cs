using Content.Shared.Atmos;

namespace Content.Shared._Starlight.Atmos;

[RegisterComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class IntermittentGasEmitterComponent : Component
{
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

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan LastEmit = TimeSpan.FromSeconds(0);
}
