using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Power.CombustionTurbine;

[RegisterComponent]
public sealed partial class CombustionTurbineGeneratorComponent : Component
{
    [ViewVariables]
    public EntityUid? Compressor;

    [DataField]
    public float LoadCoefficient = 0.0182f;

    [DataField]
    public float PowerSmoothing = 0.3f;

    [DataField]
    public float RunningVisualThreshold = 1000f;

    [ViewVariables]
    public float LastGeneration;

    [DataField]
    public ProtoId<SinkPortPrototype> ConsolePort = "CombustionTurbineConsoleReceiver";
}
