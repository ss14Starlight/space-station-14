using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Overlay.Components;

/// <summary>
/// Shows icons granted by implants if they match the type.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShowImplantedIconsComponent : Component
{
    /// <summary>
    /// The 'type' of icon that this can show, needs to match with <see cref="IconImplantComponent"/> to work
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<string> ShownIcons = default!;
}
