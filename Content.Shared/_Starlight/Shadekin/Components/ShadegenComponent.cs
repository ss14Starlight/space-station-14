using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Shadekin;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class ShadegenComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 8f;

    [DataField]
    public bool DestroyLights = false;

    [ViewVariables(VVAccess.ReadOnly), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
    public TimeSpan UpdateCooldown = TimeSpan.FromSeconds(1f);
}
