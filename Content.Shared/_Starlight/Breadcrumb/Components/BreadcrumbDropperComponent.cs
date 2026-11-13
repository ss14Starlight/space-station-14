using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Breadcrumb.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BreadcrumbDropperComponent : Component
{
    /// <summary>
    /// The color of this bag and its droppings.
    /// Black is treated as "pick a random color on startup".
    /// </summary>
    [DataField, AutoNetworkedField] public Color Color = Color.Black;

    /// <summary>
    /// The preset colors offered in the alt-click color picker, keyed by name (e.g. "white" -> "#ffffff").
    /// The key doubles as the loc-id suffix for the dropdown label: breadcrumb-dropper-color-{key}.
    /// </summary>
    [DataField, AutoNetworkedField] public Dictionary<string, Color> ColorOptions = new();

    /// <summary>
    /// Whether it's actively dropping.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Enabled;

    /// <summary>
    /// Range between the dropper and the nearest dropping before considering dropping another.
    /// </summary>
    [DataField, AutoNetworkedField] public float DropRange = 4f;

    /// <summary>
    /// How often to check whether to drop. Based on practical testing. Any higher than this and the variable gaps
    /// become noticeable.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan DropCheckFrequency = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// When the next drop check is.
    /// </summary>
    [AutoNetworkedField] public TimeSpan NextDropCheck = TimeSpan.Zero;

    /// <summary>
    /// The dropping prototype to spawn.
    /// </summary>
    [DataField, AutoNetworkedField] public EntProtoId BreadcrumbPrototype = "Glowstone";

    [DataField(required: true)] public LocId LocToggleOn;
    [DataField(required: true)] public LocId LocToggleEmpty;
    [DataField(required: true)] public LocId LocToggleOff;
    [DataField(required: true)] public LocId LocEmptied;
    [DataField(required: true)] public LocId LocRecharged;
    [DataField(required: true)] public LocId LocRechargeFull;
}

[Serializable, NetSerializable]
public enum BreadcrumbDropperVisuals
{
    /// <summary>Whether the bag is actively dropping (open) or not (closed).</summary>
    Enabled,
    /// <summary>How full the bag is, as one of the <see cref="BreadcrumbDropperFillLevel"/> buckets.</summary>
    Fill,
    /// <summary>The configured dropping color, which tints the fill layer.</summary>
    Color
}

[Serializable, NetSerializable]
public enum BreadcrumbDropperVisualLayers
{
    Base,
    Fill
}

[Serializable, NetSerializable]
public enum BreadcrumbDropperFillLevel
{
    Empty,  // no fill layer shown
    Low,    // fill-1
    Medium, // fill-2
    High    // fill-3
}
