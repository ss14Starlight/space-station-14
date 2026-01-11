using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shadekin;

#region Shadekin
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class ShadekinComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> ShadekinAlert = "Shadekin";

    [ViewVariables(VVAccess.ReadOnly), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
    public TimeSpan UpdateCooldown = TimeSpan.FromSeconds(1f);

    [AutoNetworkedField, ViewVariables]
    public ShadekinState CurrentState { get; set; } = ShadekinState.Dark;

    [DataField("thresholds", required: true)]
    public SortedDictionary<FixedPoint2, ShadekinState> Thresholds = new();
}

[Serializable, NetSerializable]
public enum ShadekinState : byte
{
    Invalid = 0,
    Dark = 1,
    Low = 2,
    Annoying = 3,
    High = 4,
    Extreme = 5

}
#endregion

#region OrganShadekinCoreComponent
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganShadekinCoreComponent : Component
{
    [DataField]
    public EntityUid? OrganOwner;

    [DataField]
    public bool Damaged = true;

    [DataField]
    public double DmagedPrice = 200;

    [DataField]
    public double UndmagedPrice = 30000;
}
#endregion

#region Brighteye
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BrighteyeComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> BrighteyeAlert { get; set; } = "ShadekinEnergy";

    [DataField]
    public ProtoId<AlertPrototype> PortalAlert { get; set; } = "ShadekinPortalAlert";

    [DataField]
    public ProtoId<AlertPrototype> RejuvenationAlert { get; set; } = "ShadekinRejuvenateAlert";

    /// <summary>
    /// How many Energy the brighteye has.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Energy = 0;

    /// <summary>
    /// The Max Energy the brighteye can have.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxEnergy = 200;

    /// <summary>
    /// Shadekin Rejuvenating Bool
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Rejuvenating = false;

    /// <summary>
    /// Shadekin Portal, if null then the portal does not exist.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Portal;

    [DataField]
    public EntityUid? PortalAction;

    [DataField]
    public EntityUid? PhaseAction;

    [DataField]
    public EntProtoId BrighteyePortalAction = "BrighteyePortalAction";

    [DataField]
    public EntProtoId BrighteyePhaseAction = "BrighteyePhaseAction";

    [DataField]
    public int PortalCost = 150;

    [DataField]
    public int PhaseCost = 50;

    [DataField]
    public EntProtoId ShadekinShadow = "ShadekinShadow";

    [DataField]
    public EntProtoId ShadekinPhaseInEffect2 = "ShadekinPhaseInEffect2";

    [DataField]
    public EntProtoId PortalShadekin = "PortalShadekin";
}

public sealed class OnAttemptEnergyUseEvent : CancellableEntityEventArgs
{
    /// <summary>
    /// The user attempting.
    /// </summary>
    public EntityUid User { get; }

    /// <summary>
    /// Triggers when a Brighteye attempt to use their energy.
    /// </summary>
    /// <param name="user"></param>
    public OnAttemptEnergyUseEvent(EntityUid user)
    {
        User = user;
    }
}

public sealed class OnBrighteyeRejuvenateAttemptEvent : CancellableEntityEventArgs
{
    /// <summary>
    /// The user attempting.
    /// </summary>
    public EntityUid User { get; }

    /// <summary>
    /// Triggers when a Brighteye attempt to Rejuvenate.
    /// </summary>
    /// <param name="user"></param>
    public OnBrighteyeRejuvenateAttemptEvent(EntityUid user)
    {
        User = user;
    }
}
#endregion
#region Abilities
public sealed partial class BrighteyePortalActionEvent : InstantActionEvent { }
public sealed partial class BrighteyePhaseActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed partial class PhaseDoAfterEvent : SimpleDoAfterEvent
{
    public override DoAfterEvent Clone() => this;
    public int Cost;
}
#endregion