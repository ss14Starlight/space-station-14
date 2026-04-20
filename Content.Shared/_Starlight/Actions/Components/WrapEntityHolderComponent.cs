using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent]
public sealed partial class WrapEntityHolderComponent : Component
{
    /// <summary>
    /// The entity that is currently being held by the wrapper. This is used to keep track of the entity that is being wrapped and to ensure that it is properly unwrapped when the wrapper is removed.
    /// </summary>
    [DataField]
    public EntityUid? Hold = null;

    /// <summary>
    /// How much time it takes for player to unwrap someone from web externally.
    /// </summary>
    [DataField]
    public TimeSpan UnWrapTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How much time it takes for player to unwrap themselves from web.
    /// </summary>
    [DataField]
    public TimeSpan SelfUnWrapTime = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Container that the wrapped entity will be put into.
    /// </summary>
    [DataField]
    public string ContainerId = "entity";

    public BaseContainer? Container = null;
}
