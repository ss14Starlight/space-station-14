using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Breadcrumb.Components;

/// <summary>
/// A breadcrumb dropping is like a pebble on the floor that glows a certain color.
/// It is dropped by a <see cref="BreadcrumbDropperComponent"/>. See that type for more info.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BreadcrumbDroppingComponent : Component
{
    /// <summary>
    /// The dropper that made this dropping.
    /// </summary>
    [AutoNetworkedField] public EntityUid Dropper = EntityUid.Invalid;

    /// <summary>
    /// The color of this dropping.
    /// </summary>
    [DataField, AutoNetworkedField] public Color Color = Color.White;

    [DataField, AutoNetworkedField] public float PhaseOffset;

}

[Serializable, NetSerializable]
public enum BreadcrumbDroppingVisualLayers
{
    Color
}
