using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.BloodCult.Components;

/// <summary>
/// Revive a cultist if Triggered.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ReviveCultistOnTriggerComponent : Component
{
	/// <summary>
    ///     The range at which the revive rune can detect dead targets.
    /// </summary>
    [DataField] public float ReviveRange = 0.8f;
}
