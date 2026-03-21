using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

/// <summary>
///     Marker component applied to entities that have been enthralled by a Dantalion vampire.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class VampireThrallComponent : Component
{
    /// <summary>
    ///     The vampire currently controlling this thrall
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? Master;

    [DataField]
    public FixedPoint2 HolyWaterConsumed = FixedPoint2.Zero;
    [DataField]
    public FixedPoint2 HolyWaterToBreakFree = FixedPoint2.New(30);
    [DataField]
    public ProtoId<ReagentPrototype> HolyWaterReagentId = "Holywater";
}
