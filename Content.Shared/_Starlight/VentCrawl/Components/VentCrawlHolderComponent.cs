using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.VentCrawl.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class VentCrawlHolderComponent : Component
{
    public Container Container
    {
        get => field ?? throw new InvalidOperationException("Container not initialized");
        set;
    } = null;

    [ViewVariables]
    public float StartingTime { get; set; }

    [ViewVariables]
    public float TimeLeft { get; set; }

    public bool IsMoving = false;

    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? PreviousTube { get; set; }

    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? NextTube { get; set; }

    [ViewVariables]
    [AutoNetworkedField]
    public Direction PreviousDirection { get; set; } = Direction.Invalid;

    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? CurrentTube { get; set; }

    [ViewVariables]
    public bool HasExitAction { get; set; }

    [ViewVariables]
    [AutoNetworkedField]
    public Direction CurrentDirection { get; set; } = Direction.Invalid;

    [ViewVariables]
    public bool IsExitingVentCrawls { get; set; }

    public static readonly TimeSpan CrawlDelay = TimeSpan.FromSeconds(0.5);

    [ViewVariables]
    public TimeSpan LastCrawl;

    [DataField("crawlSound")]
    public SoundCollectionSpecifier CrawlSound { get; set; } = new ("VentClaw", AudioParams.Default.WithVolume(5f));

    [DataField("travelDuration")]
    public float TravelDuration = 0.15f;

    [DataField]
    public EntProtoId<ActionComponent> ActionProto = "VentCrawlExitAction";

    public List<EntityUid> ProvidedActions = new();

    /// <summary>
    /// Current layer in manifold. Null if not in manifold.
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables]
    public int? ManifoldLayer;

    /// <summary>
    /// Previous layer in manifold.
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public int? PreviousManifoldLayer;

    /// <summary>
    /// Current progress of transition in manifold between layers.
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public float ManifoldTransitionProgress = 1f;

    /// <summary>
    /// Duration of transition in manifold between layers.
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public float ManifoldTransitionDuration = 0.15f;

    public TimeSpan ManifoldLayerSelectionCooldown = TimeSpan.FromSeconds(0.5f);

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan ManifoldLastLayerSelection;
}

public sealed partial class ExitVentActionEvent : InstantActionEvent
{
}
