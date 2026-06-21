using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.VentCrawl.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class VentCrawlHolderComponent : Component
{
    #region State

    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid ContainedEntity { get; set; }

    [AutoNetworkedField]
    public bool IsMoving = false;

    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? PreviousTube { get; set; }

    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? NextTube { get; set; }

    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? CurrentTube { get; set; }

    [ViewVariables]
    [AutoNetworkedField]
    public Direction CurrentDirection { get; set; } = Direction.Invalid;

    [ViewVariables]
    [AutoNetworkedField]
    public Direction PreviousDirection { get; set; } = Direction.Invalid;

    [ViewVariables]
    public bool IsExitingVentCrawls { get; set; }

    #endregion

    #region CrawlInfo

    [ViewVariables]
    public TimeSpan LastCrawl;

    [ViewVariables]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan MoveStartTime;

    [ViewVariables]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan MoveEndTime;

    /// <summary>
    /// World-space position at the start of the current move. Used for seamless interpolation.
    /// </summary>
    [AutoNetworkedField]
    public Vector2 MoveFromWorldPos;

    /// <summary>
    /// World-space position at the end of the current move. Used for seamless interpolation.
    /// </summary>
    [AutoNetworkedField]
    public Vector2 MoveToWorldPos;

    #endregion

    #region Action

    public EntProtoId<ActionComponent> ActionProto = "VentCrawlExitAction";

    [AutoNetworkedField]
    public EntityUid? ProvidedAction;

    #endregion

    #region Manifold

    /// <summary>
    /// Current layer in manifold. Null if not in manifold.
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public AtmosPipeLayer? ManifoldLayer;

    /// <summary>
    /// Previous layer in manifold.
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public AtmosPipeLayer? PreviousManifoldLayer;

    [ViewVariables]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan ManifoldTransitionStart;

    [ViewVariables]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan ManifoldTransitionEnd;

    /// <summary>
    /// Duration of transition in manifold between layers.
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public float ManifoldTransitionDuration = 0.15f;

    public TimeSpan ManifoldLayerSelectionCooldown = TimeSpan.FromSeconds(0.5f);

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan ManifoldLastLayerSelection;

    #endregion
}

public sealed partial class ExitVentActionEvent : InstantActionEvent
{
}
