using Robust.Shared.GameStates;

namespace Content.Server._Starlight.Medical;

[RegisterComponent]
public sealed partial class HealthSelfAnalyzerComponent : Component
{
    [DataField]
    public bool Toggled = false;
}
