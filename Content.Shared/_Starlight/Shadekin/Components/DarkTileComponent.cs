using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Shadekin.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class DarkTileComponent : Component
{
    [DataField]
    public float Threshold = 0.6f;

    [ViewVariables(VVAccess.ReadOnly), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
    public TimeSpan UpdateCooldown = TimeSpan.FromSeconds(3f);
}
