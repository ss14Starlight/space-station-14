using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Restrict;
[RegisterComponent, NetworkedComponent]
public sealed partial class RestrictNestingItemComponent : Component
{
    /// <summary>
    /// How many seconds it takes to pickup an item with this component
    /// </summary>
    [DataField("doAfter")]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(5.0);
}
