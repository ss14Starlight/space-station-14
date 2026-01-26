using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.BloodCult.Components;

/// <summary>
/// Spooky fella.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShadeComponent : Component
{
	/// <summary>
	/// The soulstone that this Shade originated from.
	/// When the Shade dies, the mind returns to this soulstone.
	/// </summary>
	[DataField]
	public EntityUid? SourceSoulstone;
}
