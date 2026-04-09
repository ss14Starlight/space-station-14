using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.NullSpace;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShowNullSpaceComponent : Component
{
    /// <summary>
    /// Should its show the shader of nullspace?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ShowShader = false;
}
