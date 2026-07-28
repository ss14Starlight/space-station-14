namespace Content.Shared._Starlight.Traits.Assorted;

/// <summary>
/// Marker component for the Unclonable trait.
/// Prevents this entity from being cloned in a cloning pod.
/// </summary>
[RegisterComponent]
public sealed partial class UncloneableComponent : Component
{
}
