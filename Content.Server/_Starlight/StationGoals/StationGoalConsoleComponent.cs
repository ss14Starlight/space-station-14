using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.StationGoals;

[RegisterComponent, Access(typeof(StationGoalSystem))]
public sealed partial class StationGoalConsoleComponent : Component
{
    /// <summary>
    /// Paper prototype used to print goal details.
    /// </summary>
    [DataField]
    public EntProtoId PaperPrototype = "Paper";

    /// <summary>
    /// Cooldown for printing goal details, so you can't duplicate paper from nothing.
    /// </summary>
    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Sound used when printing new goal details.
    /// </summary>
    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>
    /// Relative time, determines when next print will be available.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextPrint;
}
