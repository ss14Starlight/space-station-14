// ReSharper disable CheckNamespace
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.PDA;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    // Only enforce the Station AI-specific rule on the virtual PDA; ordinary PDAs keep their existing access behavior.
    [Dependency] private TagSystem _pdaTags = default!;

    private readonly ProtoId<TagPrototype> _stationAiTag = "StationAi";

    // Intrinsic UI actions run on their owner, and mind transfers can carry that action into a shunted body.
    // Cancel only the Station AI PDA action unless the brain is currently held by its core.
    [SubscribeLocalEvent]
    private void OnStationAiPdaToggleAttempt(IntrinsicUIOpenAttemptEvent args)
    {
        // Ignore other intrinsic UIs and entities; the shared handler should only gate this virtual PDA.
        if (!Equals(args.Key, PdaUiKey.Key) ||
            !HasComp<PdaComponent>(args.User) ||
            !_pdaTags.HasTag(args.User, _stationAiTag) ||
            CanUseStationAiPda(args.User))
            return;

        args.Cancel();
    }

    // The action guard prevents opening the PDA, while this range guard also denies an already-open UI after transfer.
    [SubscribeLocalEvent]
    private void OnStationAiPdaRangeCheck(ref BoundUserInterfaceCheckRangeEvent args)
    {
        // Scope the restriction to Station AI PDA entities so unrelated PDA and UI checks remain unchanged.
        if (!Equals(args.UiKey, PdaUiKey.Key) ||
            !HasComp<PdaComponent>(args.Target) ||
            !_pdaTags.HasTag(args.Target, _stationAiTag) ||
            CanUseStationAiPda(args.Target))
            return;

        args.Result = BoundUserInterfaceRangeResult.Fail;
    }

    // AiHeld is applied only while the brain is in a core; Inhabited is set while shunted into another body.
    private bool CanUseStationAiPda(EntityUid ai) => HasComp<StationAiHeldComponent>(ai) &&
                                                    TryComp<StationAIShuntableComponent>(ai, out var shuntable) &&
                                                    shuntable.Inhabited == null;
}
