using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Components.Classes;

[RegisterComponent, NetworkedComponent]
public sealed partial class DantalionComponent : VampireClassComponent
{
	/// <summary>
	///     Base thrall limit before blood / power bonuses
	/// </summary>
	[DataField]
	public int BaseThrallLimit = 1;

	/// <summary>
	///     Runtime tracking of enthralled entities
	/// </summary>
	[ViewVariables(VVAccess.ReadOnly)]
	public HashSet<EntityUid> Thralls = new();

	/// <summary>
	///     Total thrall slots consumed. Does not decrease when thralls are lost.
	/// </summary>
	[ViewVariables(VVAccess.ReadOnly)]
	public int ThrallSlotsUsed = 0;

	/// <summary>
	///     Whether Blood Bond is currently active
	/// </summary>
	[ViewVariables(VVAccess.ReadOnly)]
	public bool BloodBondActive = false;

	/// <summary>
	///     Loop id for Blood Bond to prevent duplicate loops
	/// </summary>
	public int BloodBondLoopId = 0;

	/// <summary>
	///     Thralls currently linked via Blood Bond
	/// </summary>
	[ViewVariables(VVAccess.ReadOnly)]
	public HashSet<EntityUid> BloodBondLinkedThralls = new();

	[ViewVariables(VVAccess.ReadOnly)]
	public bool BloodBondProcessingDamage = false;

    [DataField]
    public EntProtoId rallyOverlayEffect = "VampireRallyOverlayEffect";
    [DataField]
    public int ThrallHealBurn = 3;
    [DataField]
    public int ThrallHealBrute = 3;
    [DataField]
    public int ThrallHealAsphyxiation = 5;
    [DataField]
    public int ThrallLevel2Blood = 400;
    [DataField]
    public int ThrallLevel3Blood = 600;
    [DataField]
    public int HealBloodThreshold = 300;
}
