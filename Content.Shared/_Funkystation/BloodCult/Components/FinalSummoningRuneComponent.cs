using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.BloodCult.Components;

/// <summary>
/// Marks a rune as part of the final summoning ritual.
/// Three of these runes surround the rift and must be activated simultaneously.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FinalSummoningRuneComponent : Component
{
	/// <summary>
	/// The rift this rune is associated with.
	/// </summary>
	[DataField]
	public EntityUid? RiftUid = null;
}

