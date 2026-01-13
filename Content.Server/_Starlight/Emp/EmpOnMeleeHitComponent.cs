namespace Content.Server._Starlight.Emp;

/// <summary>
/// Upon hitting an object will EMP area around it.
/// </summary>
[RegisterComponent]
[Access(typeof(Content.Server.Emp.EmpSystem))]
public sealed partial class EmpOnMeleeHitComponent : Component
{
    [DataField]
    public float Range = 1.0f;

    /// <summary>
    /// How much energy will be consumed per battery in range
    /// </summary>
    [DataField]
    public float EnergyConsumption;

    /// <summary>
    /// How long it disables targets in seconds
    /// </summary>
    [DataField]
    public TimeSpan DisableDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How much battery power it takes to trigger the EMP
    /// </summary>
    [DataField]
    public float EnergyPerUse = 0f;

    /// <summary>
    /// Does the EMP disable the item?
    /// </summary>
    [DataField]
    public bool DisableOnHit = true;


}
