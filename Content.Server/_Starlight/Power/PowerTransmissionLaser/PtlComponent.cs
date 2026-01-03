using Robust.Shared.Audio;

namespace Content.Server._Starlight.Power.PowerTransmissionLaser;

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
    public float MaxPowerMw = 10f;

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

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    [DataField]
    public SoundSpecifier LoopingSound = new SoundPathSpecifier("/Audio/Weapons/ebladehum.ogg");

    [ViewVariables]
    public EntityUid? PlayingStream;
}
