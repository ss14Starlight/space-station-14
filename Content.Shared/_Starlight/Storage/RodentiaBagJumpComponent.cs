namespace Content.Shared._Starlight.Storage;

[RegisterComponent]
public sealed partial class RodentiaBagJumpComponent : Component
{
    [DataField]
    public TimeSpan GroundBagDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan AttachedBagDelay = TimeSpan.FromSeconds(5);
}
