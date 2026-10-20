using Content.Shared.Atmos;

namespace Content.Shared._Starlight.GasMining.GasNode;

[RegisterComponent]
public sealed partial class GasNodeComponent : Component
{
    [DataField]
    public Dictionary<Gas, float> Gases = new();

    [DataField]
    public float Temperature = 273.15f;
}
