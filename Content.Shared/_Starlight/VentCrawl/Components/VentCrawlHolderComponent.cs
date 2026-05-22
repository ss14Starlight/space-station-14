using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.VentCrawl.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class VentCrawlHolderComponent : Component
{
    private Container? _container = null;
    public Container Container
    {
        get => _container ?? throw new InvalidOperationException("Container not initialized");
        set => _container = value;
    }

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
    public Direction CurrentDirection { get; set; } = Direction.Invalid;

    [ViewVariables]
    public bool IsExitingVentCrawls { get; set; }

    public static readonly TimeSpan CrawlDelay = TimeSpan.FromSeconds(0.5);

    public TimeSpan LastCrawl;

    [DataField("crawlSound")]
    public SoundCollectionSpecifier CrawlSound { get; set; } = new ("VentClaw", AudioParams.Default.WithVolume(5f));

    [DataField("travelDuration")]
    public float TravelDuration = 0.15f;

    [DataField]
    public EntProtoId<ActionComponent> ActionProto = "VentCrawlExitAction";

    public List<EntityUid> ProvidedActions = new();
}

public sealed partial class ExitVentActionEvent : InstantActionEvent
{
}
