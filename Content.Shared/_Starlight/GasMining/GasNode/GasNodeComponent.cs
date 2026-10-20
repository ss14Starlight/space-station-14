using Content.Shared.Atmos;

namespace Content.Shared._Starlight.GasMining.GasNode;

[RegisterComponent]
public sealed partial class GasNodeCompoment : Component
{
    [DataField]
    public Dictionary<Gas, float> Gases;

    [DataField]
    public float Temperature = 273.15f;
}
