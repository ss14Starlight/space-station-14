using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

/// <summary>
///     Marker component applied to entities that have been enthralled by a Dantalion vampire.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[AutoGenerateComponentState]
public sealed partial class VampireThrallComponent : BaseMindRoleComponent
{
    /// <summary>
    ///     The vampire currently controlling this thrall
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Master;

    [DataField]
    public FixedPoint2 HolyWaterConsumed = FixedPoint2.Zero;

    [DataField]
    public FixedPoint2 HolyWaterToBreakFree = FixedPoint2.New(30);

    [DataField]
    public ProtoId<ReagentPrototype> HolyWaterReagentId = "Holywater";

    [DataField]
    public TimeSpan DeconvertStunDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan BreakFreeCheckInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public EntProtoId ObeyObjectiveId = "VampireThrallObeyMasterObjective";

    [DataField]
    public EntProtoId MindRoleId = "MindRoleThrall";

    [ViewVariables]
    [AutoPausedField]
    public TimeSpan NextBreakFreeCheck;
}
