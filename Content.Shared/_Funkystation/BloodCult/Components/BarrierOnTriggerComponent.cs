using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.BloodCult.Components;

/// <summary>
/// Spawn barrier chain if Triggered.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BarrierOnTriggerComponent : Component
{
	/// <summary>
    ///     The entity to spawn (e.g. animation) while carving.
    /// </summary>
    //[DataField] public string InProgress = "Flash";

	/// <summary>
    ///     The entity to spawn when used on self.
    /// </summary>
    //[DataField, AutoNetworkedField] public string Rune = "BarrierRune";

	/// <summary>
    ///     Damage to apply to self for each barrier activated.
    /// </summary>
    [DataField] public int DamageOnActivate = 2;

	/// <summary>
    ///     Time in seconds needed to carve a rune.
    /// </summary>
    //[DataField] public float TimeToCarve = 6f;

	/// <summary>
    ///     Sound that plays when used to carve a rune.
    /// </summary>
    //[DataField] public SoundSpecifier CarveSound = new SoundCollectionSpecifier("gib");

	/// <summary>
	/// 	Current user using the knife
	/// </summary>
	//[ViewVariables]
	//public EntityUid? User;
}
