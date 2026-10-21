using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.GasMining.GasExtractor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GasExtractorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = false;

    [DataField("outlet")]
    public string OutletName = "pipe";

    [DataField]
    public float Volume = 1500f;

    [DataField]
    public float MolesExtractedPerSecond = 200f;

    [DataField]
    public float MaxBufferPressure = Atmospherics.MaxOutputPressure * 2;
}
