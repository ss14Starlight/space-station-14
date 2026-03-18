using Robust.Shared.GameStates;

#region Starlight
using Content.Shared.Genetics;
#endregion Starlight

namespace Content.Shared.Movement.Components;

/// <summary>
/// Special component to allow an entity to navigate kudzu without slowdown.
/// </summary>
[RegisterComponent, NetworkedComponent]
[GeneticComponent(2, 4)] // Starlight
public sealed partial class IgnoreKudzuComponent : Component
{
}
