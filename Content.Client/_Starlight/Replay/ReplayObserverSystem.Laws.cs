using Content.Shared._Starlight.Replay;
using Content.Shared._Starlight.Thaven;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;

namespace Content.Client._Starlight.Replay;

// Silicon laws and thaven moods. Their windows are filled from recorded UI state, which only exists if the owner
// opened the window, so the state is rebuilt from ReplayLawsComponent where the recording has one.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedStationAiSystem _stationAi = default!;

    private void InitializeLaws() =>
        SubscribeLocalEvent<ReplayLawsComponent, AfterAutoHandleStateEvent>(OnReplayLawsUpdated);

    private void AddLawVerbs(GetVerbsEvent<Verb> ev)
    {
        if (GetLawBearer(ev.Target) is { } bearer)
            AddViewerVerb(ev, SiliconLawsUiKey.Key, "replay-observer-verb-view-laws", "information.svg.192dpi.png", bearer);

        if (HasComp<ThavenMoodsComponent>(ev.Target))
            AddViewerVerb(ev, ThavenMoodsUiKey.Key, "replay-observer-verb-view-moods", "information.svg.192dpi.png");
    }

    // A station AI's laws are on the brain inside its core, which can't be clicked; the core or AI eye stands in for it.
    private EntityUid? GetLawBearer(EntityUid target)
    {
        if (HasComp<SiliconLawBoundComponent>(target))
            return target;

        var holder = target;
        var cores = AllEntityQuery<StationAiCoreComponent>();
        while (cores.MoveNext(out var core, out var coreComp))
        {
            if (coreComp.RemoteEntity != target)
                continue;

            holder = core;
            break;
        }

        if (!_stationAi.TryGetHeld(new Entity<StationAiHolderComponent?>(holder, null), out var held) || !HasComp<SiliconLawBoundComponent>(held))
            return null;

        return held;
    }

    private void ApplyLawState(EntityUid target, Enum key)
    {
        if (!TryComp<ReplayLawsComponent>(target, out var laws))
            return;

        if (key is SiliconLawsUiKey && laws.SiliconLaws != null)
            _ui.SetUiState(target, key, new SiliconLawBuiState(new(laws.SiliconLaws), null, null));
        else if (key is ThavenMoodsUiKey && laws.SharedMoods != null)
            _ui.SetUiState(target, key, new ThavenMoodsBuiState(new(laws.SharedMoods)));
    }

    // Keep an open window current as the recording's laws change.
    private void OnReplayLawsUpdated(Entity<ReplayLawsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_observer is not { } observer)
            return;

        if (_ui.IsUiOpen(ent.Owner, SiliconLawsUiKey.Key, observer))
            ApplyLawState(ent, SiliconLawsUiKey.Key);

        if (_ui.IsUiOpen(ent.Owner, ThavenMoodsUiKey.Key, observer))
            ApplyLawState(ent, ThavenMoodsUiKey.Key);
    }
}
