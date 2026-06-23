using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Overlay.Components;

/// <summary>
/// Shows granted icons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImplantedIconComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<FactionIconPrototype>? Icon;

    [DataField, AutoNetworkedField]
    public string IconType = string.Empty;
}
