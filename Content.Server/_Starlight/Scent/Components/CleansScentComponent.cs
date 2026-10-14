namespace Content.Server._Starlight.Scent.Components;

// Items that clean scent evidence off things, or wash a scent-having entity's own ScentId away.
// Mirrors CleansForensicsComponent but kept separate.
[RegisterComponent]
public sealed partial class CleansScentComponent : Component
{
    [DataField]
    public float CleanDelay = 6.0f;

    [DataField]
    public float MovementThreshold = 0.01f;
}
