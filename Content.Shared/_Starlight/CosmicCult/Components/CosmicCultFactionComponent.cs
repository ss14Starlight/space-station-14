using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CosmicCult.Components;
/// <summary>
/// Marker component for Cosmic Cult-aligned entities.
/// Used to identify hostile Cosmic entities separately from player cultists,
/// allowing systems such as faction icon visibility to recognize them as Cosmic Cult-affiliated
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CosmicCultFactionComponent : Component
{
}
