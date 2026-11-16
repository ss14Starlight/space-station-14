using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Daiquiri.Planets;

[RegisterComponent, NetworkedComponent]
public sealed partial class PlanetConsoleComponent : Component
{
    [DataField]
    public List<ProtoId<PlanetDestinationPrototype>> Planets = new();

    [DataField]
    public TimeSpan PrintTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(20);

    [DataField]
    public SoundSpecifier PrintSound = new SoundCollectionSpecifier("GeneratorTug");

    [DataField]
    public SoundSpecifier PrintLoopSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");

    [ViewVariables]
    public EntityUid? PrintStream;

    [ViewVariables]
    public string? Printing;

    [ViewVariables]
    public TimeSpan PrintEnd;
}
