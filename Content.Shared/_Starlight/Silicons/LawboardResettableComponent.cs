using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons;

[RegisterComponent, NetworkedComponent]
public sealed partial class LawboardResettableComponent : Component
{
    [DataField]
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(10.0f);
}