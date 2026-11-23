using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.StationGoals;

[RegisterComponent, Access(typeof(StationGoalSystem))]
public sealed partial class StationGoalConsoleComponent : Component
{
    [DataField]
    public EntProtoId PaperPrototype = "Paper";

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [ViewVariables]
    public TimeSpan NextPrint;
}
