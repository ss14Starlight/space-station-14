using Content.Shared._Starlight.Replay;
using Content.Shared._Starlight.Thaven;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Verbs;

namespace Content.Client._Starlight.Replay;

// Silicon laws and thaven moods. Their windows are filled from recorded UI state, which only exists if the owner
// opened the window, so the state is rebuilt from ReplayLawsComponent where the recording has one.
public sealed partial class ReplayObserverSystem
{
    private void InitializeLaws() =>
        SubscribeLocalEvent<ReplayLawsComponent, AfterAutoHandleStateEvent>(OnReplayLawsUpdated);

    private void AddLawVerbs(GetVerbsEvent<Verb> ev)
    {
        if (HasComp<SiliconLawBoundComponent>(ev.Target))
            AddViewerVerb(ev, SiliconLawsUiKey.Key, "replay-observer-verb-view-laws", "information.svg.192dpi.png");

        if (HasComp<ThavenMoodsComponent>(ev.Target))
            AddViewerVerb(ev, ThavenMoodsUiKey.Key, "replay-observer-verb-view-moods", "information.svg.192dpi.png");
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
