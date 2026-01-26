using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.BloodCult.Components;

/// <summary>
/// Summon structures when triggered with the appropriate material stacks.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SummonOnTriggerComponent : Component
{
	/// <summary>
    ///     The range at which the summon rune can find material stacks.
    /// </summary>
    [DataField] public float SummonRange = 0.3f;
}
