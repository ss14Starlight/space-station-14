namespace Content.Shared._Starlight.Footprints;

/// <summary>
/// Goes alongside FootprintOwnerComponent. Makes standing prints use the paw sprite instead
/// of the default foot one. Doesn't affect dragged/downed body prints, which aren't
/// species-specific.
/// </summary>
[RegisterComponent]
public sealed partial class PawPrintsComponent : Component
{
}
