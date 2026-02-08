namespace Content.Shared._Starlight.Terminator;

[RegisterComponent]
public sealed partial class TerminatorSpawnTargetComponent : Component
{
    [DataField]
    public EntityUid? Target;
}