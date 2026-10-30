using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

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
    [DataField, AutoNetworkedField] public float DropRange = 3.5f;

    /// <summary>
    /// How often to check whether to drop.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan DropCheckFrequency = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// When the next drop check is.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan NextDropCheck = TimeSpan.Zero;

    /// <summary>
    /// The dropping prototype to spawn.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId DroppingPrototype = "BreadcrumbDropping";

    /// <summary>
    /// The index of the next dropping. This is basically jut an infinitely increasing counter.
    /// </summary>
    [DataField, AutoNetworkedField] public int NextIndex = 0;

    /// <summary>
    /// By how much to increase the lighting phase per index.
    /// </summary>
    [DataField, AutoNetworkedField] public float IndexPhaseMultiplier = 0.07f;
}
