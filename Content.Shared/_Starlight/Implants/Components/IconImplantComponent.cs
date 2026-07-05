using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.Implants.Components;

/// <summary>
/// Sets the icon shown by the <see cref="ImplantedIconComponent"/> when implanted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IconImplantComponent : Component
{
    /// <summary>
    /// The icon prototype to use
    /// </summary>
    [DataField("icon", customTypeSerializer: typeof(PrototypeIdSerializer<FactionIconPrototype>), required: true), ViewVariables(VVAccess.ReadWrite)]
    public string Icon;

    /// <summary>
    /// The 'type' of icon that this is - needs to match a string in <see cref="ShowImplantedIconsComponent"/>
    /// </summary>
    [DataField(required: true)]
    public string IconType = default!;
}
