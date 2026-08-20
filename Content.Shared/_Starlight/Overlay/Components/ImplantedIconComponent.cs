using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Overlay.Components;

/// <summary>
/// Shows the icon granted by the <see cref="IconImplantComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImplantedIconComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<FactionIconPrototype>? Icon;

    [DataField, AutoNetworkedField]
    public string IconType = string.Empty;
}
