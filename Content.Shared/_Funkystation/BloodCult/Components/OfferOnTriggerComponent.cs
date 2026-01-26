using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.BloodCult.Components;

/// <summary>
/// Offer a non-cultist if Triggered.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OfferOnTriggerComponent : Component
{
	/// <summary>
    ///     The range at which the offer rune can function.
    /// </summary>
    [DataField] public float OfferRange = 0.2f;

	/// <summary>
	///	    The range at which cultists can contribute to an invocation.
	/// </summary>
	[DataField] public float InvokeRange = 1.4f;
}
