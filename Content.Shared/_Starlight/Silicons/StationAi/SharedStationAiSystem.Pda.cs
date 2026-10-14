// ReSharper disable CheckNamespace
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.PDA;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    [Dependency] private TagSystem _pdaTags = default!;

    private readonly ProtoId<TagPrototype> _stationAiTag = "StationAi";

    // The toggle action can follow the AI's mind when it shunts, so only allow it while the AI is in its core.
    [SubscribeLocalEvent]
    private void OnStationAiPdaToggleAttempt(IntrinsicUIOpenAttemptEvent args)
    {
        if (!Equals(args.Key, PdaUiKey.Key) ||
            !HasComp<PdaComponent>(args.User) ||
            CanUseStationAiPda(args.User))
            return;

        args.Cancel();
    }

    // Close off the bound UI as well, including an interface that was already open when the AI transferred.
    [SubscribeLocalEvent]
    private void OnStationAiPdaRangeCheck(ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (!Equals(args.UiKey, PdaUiKey.Key) ||
            !HasComp<PdaComponent>(args.Target) ||
            !_pdaTags.HasTag(args.Target, _stationAiTag) ||
            CanUseStationAiPda(args.Target))
            return;

        args.Result = BoundUserInterfaceRangeResult.Fail;
    }

    private bool CanUseStationAiPda(EntityUid ai) => HasComp<StationAiHeldComponent>(ai) &&
               TryComp<StationAIShuntableComponent>(ai, out var shuntable) &&
               shuntable.Inhabited == null;
}
