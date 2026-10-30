using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Power.CombustionTurbine;

[RegisterComponent]
public sealed partial class CombustionTurbineConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Compressor;

    [DataField]
    public ProtoId<SourcePortPrototype> LinkingPort = "CombustionTurbineConsoleSender";

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextUpdate;
}
