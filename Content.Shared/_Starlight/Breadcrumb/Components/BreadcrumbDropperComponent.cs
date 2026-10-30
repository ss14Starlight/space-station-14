using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Breadcrumb.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BreadcrumbDropperComponent : Component
{
    /// <summary>
    /// The color of this bag and its droppings.
    /// </summary>
    [DataField, AutoNetworkedField] public Color Color = Color.White;

    /// <summary>
    /// Whether it's actively dropping.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Enabled;

    /// <summary>
    /// Range between the dropper and the nearest dropping before considering dropping another.
    /// </summary>
    [DataField, AutoNetworkedField] public float DropRange = 4f;

    /// <summary>
    /// How often to check whether to drop.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan DropCheckFrequency = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// When the next drop check is.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan NextDropCheck = TimeSpan.Zero;

    /// <summary>
    /// The dropping prototype to spawn.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId BreadcrumbPrototype = "Glowstone";
}
