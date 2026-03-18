using Robust.Shared.GameStates;

#region Starlight
using Content.Shared.Genetics;
#endregion Starlight

namespace Content.Shared.RatKing.Components;

/// <summary>
/// This is used for entities that can rummage through entities
/// with the <see cref="RummageableComponent"/>
/// </summary>
///
[RegisterComponent, NetworkedComponent]
[GeneticComponent(5, 5)] // Starlight
public sealed partial class RummagerComponent : Component;
