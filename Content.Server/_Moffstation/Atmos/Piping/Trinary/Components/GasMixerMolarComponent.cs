using Content.Shared.Atmos;

namespace Content.Server._Moffstation.Atmos.Piping.Trinary.Components;


/// <summary>
/// A Gas Mixer variant which mix gases by mols rather than pressure
/// </summary>
[RegisterComponent]
public sealed partial class GasMixerMolarComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField("inletOne")]
    public string InletOneName = "inletOne";

    [DataField("inletTwo")]
    public string InletTwoName = "inletTwo";

    [DataField("outlet")]
    public string OutletName = "outlet";

    [DataField]
    public float TargetPressure = Atmospherics.OneAtmosphere;

    [DataField]
    public float MaxTargetPressure = Atmospherics.MaxOutputPressure;

    [DataField]
    public float InletOneConcentration = 0.5f;

    [DataField]
    public float InletTwoConcentration = 0.5f;
}
