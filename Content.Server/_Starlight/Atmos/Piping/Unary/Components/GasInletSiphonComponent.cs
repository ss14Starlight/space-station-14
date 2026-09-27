using Content.Shared.Atmos;
using Content.Shared.Guidebook;

namespace Content.Server._Starlight.Atmos.Piping.Unary.Components;

[RegisterComponent]
public sealed partial class GasInletSiphonComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public float TransferRate
    {
        get => _transferRate;
        set => _transferRate = Math.Clamp(value, 0f, MaxTransferRate);
    }

    private float _transferRate = 200;

    [DataField]
    public float MaxTransferRate = Atmospherics.MaxTransferRate;

    [DataField]
    [GuidebookData]
    public float MaxPressure = Atmospherics.MaxOutputPressure;

    [DataField("outlet")]
    public string OutletName = "pipe";
}
