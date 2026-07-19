using Content.Shared.MedicalScanner;

namespace Content.Shared._Sol.Medical.Virology.Events;

/// <summary>
/// Raised after base health analyzer state is built so Sol can append organ/debug data.
/// </summary>
[ByRefEvent]
public struct HealthAnalyzerVirologyFillEvent
{
    public EntityUid Target;
    public HealthAnalyzerUiState State;
    public bool Debug;

    public HealthAnalyzerVirologyFillEvent(EntityUid target, HealthAnalyzerUiState state, bool debug)
    {
        Target = target;
        State = state;
        Debug = debug;
    }
}
