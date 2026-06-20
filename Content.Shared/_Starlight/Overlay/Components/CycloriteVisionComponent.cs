using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Overlay.Components;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CycloriteVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = true;
}
