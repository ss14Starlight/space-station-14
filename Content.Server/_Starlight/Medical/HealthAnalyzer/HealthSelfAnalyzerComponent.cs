namespace Content.Server._Starlight.Medical.HealthAnalyzer;

[RegisterComponent]
public sealed partial class HealthSelfAnalyzerComponent : Component
{
    [DataField]
    public bool Toggled = false;
}
