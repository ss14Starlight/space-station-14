using Robust.Shared.GameStates;

namespace Content.Server._Starlight.PowerTransmissionLaser;

[RegisterComponent]
public sealed partial class PtlComponent : Component
{
    [DataField]
    public bool Enabled;

    [DataField]
    public float TargetPowerMw = 1f;

    [DataField]
    public float MinPowerMw = 0f;

    [DataField]
    public float MaxPowerMw = 5f;

    [DataField]
    public float CycleTimeSeconds = 2f;

    [DataField]
    public float SpesosPerMwPerCycle = 50f;

    [ViewVariables]
    public int TotalSpesosEarned;

    [ViewVariables]
    public double SpesoCarry;

    [ViewVariables]
    public float Accumulator;
}
