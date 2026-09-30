using Content.Client.Verbs.UI;
using Content.Shared.Paper;
using Content.Shared.Storage;
using Content.Shared.Strip.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Replay;

// Opens the real strip, storage and paper UIs as the observer. Ghosts may open UIs but not act in them.
// Recorded UI state closes windows whenever someone in the recording opens or closes the same UI, so
// windows are reopened until the viewer closes them. Reopening runs before the UI system processes its close queue, so
// the window survives rather than flickering.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private readonly HashSet<(EntityUid Target, Enum Key)> _viewing = new();
    private readonly List<(EntityUid Target, Enum Key)> _viewingScratch = new();

    private void InitializeViewer()
    {
        UpdatesBefore.Add(typeof(SharedUserInterfaceSystem));

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetViewerVerbs);
        SubscribeLocalEvent<CloseBoundInterfaceMessage>(OnUiClosedByViewer);
    }

    private void OnGetViewerVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!IsReplayActive || ev.User != _observer)
            return;

        if (HasComp<StrippableComponent>(ev.Target))
            AddViewerVerb(ev, StrippingUiKey.Key, "replay-observer-verb-view-inventory", "outfit.svg.192dpi.png");

        if (HasComp<StorageComponent>(ev.Target))
            AddViewerVerb(ev, StorageComponent.StorageUiKey.Key, "replay-observer-verb-view-contents", "open.svg.192dpi.png");

        if (HasComp<PaperComponent>(ev.Target))
            AddViewerVerb(ev, PaperComponent.PaperUiKey.Key, "replay-observer-verb-read", "examine.svg.192dpi.png");
    }

    private void AddViewerVerb(GetVerbsEvent<Verb> ev, Enum key, string loc, string icon)
    {
        var target = ev.Target;
        if (!_ui.HasUi(target, key))
            return;

        ev.Verbs.Add(new Verb
        {
            Text = Loc.GetString(loc),
            Icon = new SpriteSpecifier.Texture(new ResPath($"/Textures/Interface/VerbIcons/{icon}")),
            Act = () => OpenView(target, key),
            ClientExclusive = true,
            Priority = 10,
        });
    }

    public bool TryViewContents(EntityUid target)
    {
        if (!HasComp<StorageComponent>(target) || !_ui.HasUi(target, StorageComponent.StorageUiKey.Key))
            return false;

        OpenView(target, StorageComponent.StorageUiKey.Key);
        return true;
    }

    public void OpenVerbMenu(EntityUid target) =>
        _uiManager.GetUIController<VerbMenuUIController>().OpenVerbMenu(target);

    private void OpenView(EntityUid target, Enum key)
    {
        if (_observer is not { } observer || observer != _player.LocalEntity)
            return;

        // The replicated storage limit only allows one bag open at a time, as in live play.
        if (key is StorageComponent.StorageUiKey)
        {
            _viewingScratch.Clear();
            _viewingScratch.AddRange(_viewing);
            foreach (var view in _viewingScratch)
            {
                if (view.Key is not StorageComponent.StorageUiKey || view.Target == target)
                    continue;

                _viewing.Remove(view);
                _ui.CloseUi(view.Target, view.Key, observer);
            }
        }

        _ui.OpenUi(target, key, observer);

        if (_ui.IsUiOpen(target, key, observer))
            _viewing.Add((target, key));
    }

    // Only raised for closes sent as messages, i.e. by the viewer. Closes from recorded state bypass it.
    private void OnUiClosedByViewer(CloseBoundInterfaceMessage args)
    {
        if (args.Actor == _observer)
            _viewing.Remove((GetEntity(args.Entity), args.UiKey));
    }

    private void UpdateViewer()
    {
        if (_viewing.Count == 0)
            return;

        if (_observer is not { } observer || observer != _player.LocalEntity || !Exists(observer))
        {
            _viewing.Clear();
            return;
        }

        _viewingScratch.Clear();
        _viewingScratch.AddRange(_viewing);
        foreach (var (target, key) in _viewingScratch)
        {
            if (!Exists(target))
            {
                _viewing.Remove((target, key));
                continue;
            }

            if (_ui.IsUiOpen(target, key, observer))
                continue;

            _ui.OpenUi(target, key, observer);

            if (!_ui.IsUiOpen(target, key, observer))
                _viewing.Remove((target, key));
        }
    }
}
