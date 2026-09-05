using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CosmicCult.Components;
/// <summary>
/// Marker component for Cosmic Cult minions.
///
/// Unlike <see cref="CosmicCultComponent"/>, this does not make the entity a
/// cult member and does not grant Cosmic Cult abilities or objectives.
///
/// It is used by YAML prototypes to identify minion entities, primarily for
/// faction icon visibility and other entity recognition systems.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CosmicMinionComponent : Component
{
}
