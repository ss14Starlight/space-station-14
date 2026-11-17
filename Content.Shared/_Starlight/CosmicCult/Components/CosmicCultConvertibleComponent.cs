using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CosmicCult.Components;

/// <summary>
/// Allows non-humanoids with this component to be converted by the cosmic cult.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CosmicCultConvertibleComponent : Component
{
    /// <summary>
    /// Must be true for conversion and ability targeting to accept the entity.
    /// Lets specific prototypes opt out while inheriting this component.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Convertible = true;
}
