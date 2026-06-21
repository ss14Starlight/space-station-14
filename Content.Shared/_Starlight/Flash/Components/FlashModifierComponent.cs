using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Flash.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlashModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Modifier = 1f;
}
