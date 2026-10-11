using Content.Client.Storage;
using Content.Client.Verbs.UI;
using Content.Shared.Chemistry;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Storage;
using Content.Shared.Strip.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Replay;

// Opens real UIs (inventory, bags, paper, laws) as the observer. Ghosts may open UIs but not act in them.
// Recorded UI state closes windows whenever someone in the recording opens or closes the same UI, so
// windows are reopened until the viewer closes them. Reopening runs just before the UI system's update processes its
// close queue, so the window survives rather than flickering.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    // Machines whose UI state the server pushes on every change, so recordings always have it.
    private static readonly Enum[] _machineViewKeys = [ChemMasterUiKey.Key, ReagentDispenserUiKey.Key];

    private readonly HashSet<(EntityUid Target, Enum Key)> _viewing = new();
    private readonly List<(EntityUid Target, Enum Key)> _viewingScratch = new();

    private void InitializeViewer()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetViewerVerbs);
        SubscribeLocalEvent<CloseBoundInterfaceMessage>(OnUiClosedByViewer);

        // Replays run without prediction, so the handler has to opt in to firing outside it.
        CommandBinds.Builder
            .BindBefore(ContentKeyFunctions.ActivateItemInWorld,
                new PointerInputCmdHandler(OnActivateInWorld, outsidePrediction: true),
                typeof(SharedInteractionSystem))
            .Register<ReplayObserverSystem>();
    }

    private void ShutdownViewer() => CommandBinds.Unregister<ReplayObserverSystem>();

    /// <summary>
    /// Whether the viewer can open windows: a replay is playing and the local entity is the free observer.
    /// </summary>
    public bool IsViewing() => IsReplayActive && _observer is { } observer && observer == _player.LocalEntity;

    private bool OnActivateInWorld(ICommonSession? session, EntityCoordinates coords, EntityUid uid) =>
        IsViewing() && uid.IsValid() && Exists(uid) && TryOpenDefaultView(uid);

    /// <summary>
    /// Opens the most useful view for an entity: inventory for mobs, contents for storage, text for paper.
    /// </summary>
    public bool TryOpenDefaultView(EntityUid target)
    {
        if (HasComp<StrippableComponent>(target) && _ui.HasUi(target, StrippingUiKey.Key))
        {
            OpenView(target, StrippingUiKey.Key);
            return true;
        }

        // Before storage: dispensers also hold their jugs in a storage grid.
        foreach (var key in _machineViewKeys)
        {
            if (!_ui.HasUi(target, key))
                continue;

            OpenView(target, key);
            return true;
        }

        if (TryViewContents(target))
            return true;

        if (HasComp<PaperComponent>(target) && _ui.HasUi(target, PaperComponent.PaperUiKey.Key))
        {
            OpenView(target, PaperComponent.PaperUiKey.Key);
            return true;
        }

        return false;
    }

    private void OnGetViewerVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!IsReplayActive || ev.User != _observer)
            return;

        if (HasComp<StrippableComponent>(ev.Target))
            AddViewerVerb(ev, StrippingUiKey.Key, "replay-observer-verb-view-inventory", "outfit.svg.192dpi.png");

        if (HasComp<PaperComponent>(ev.Target))
            AddViewerVerb(ev, PaperComponent.PaperUiKey.Key, "replay-observer-verb-read", "examine.svg.192dpi.png");

        foreach (var key in _machineViewKeys)
        {
            AddViewerVerb(ev, key, "replay-observer-verb-view-contents", "open.svg.192dpi.png");
        }

        AddLawVerbs(ev);
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

        // Already open, possibly hidden behind a nested bag.
        if (_ui.IsUiOpen(target, key, observer))
        {
            // TryGetOpenUi casts without checking the UI's type, so only ask for a bag UI under the bag key.
            if (key is StorageComponent.StorageUiKey
                && _ui.TryGetOpenUi<StorageBoundUserInterface>(target, key, out var openBag))
            {
                openBag.Show();
            }

            return;
        }

        if (key is StorageComponent.StorageUiKey)
            PrepareStorageView(target, observer);

        ApplyLawState(target, key);
        ForceOpen(target, key, observer);

        if (_ui.IsUiOpen(target, key, observer))
            _viewing.Add((target, key));
    }

    // Mirrors live storage: a bag inside an open bag hides its parent (the child's back button shows it again), and
    // any other bag replaces whatever bags are open.
    private void PrepareStorageView(EntityUid target, EntityUid observer)
    {
        if (_containers.TryGetContainingContainer(target, out var container)
            && TryComp<StorageComponent>(container.Owner, out var parentStorage)
            && parentStorage.Container.Contains(target)
            && _ui.TryGetOpenUi<StorageBoundUserInterface>(container.Owner, StorageComponent.StorageUiKey.Key, out var parentBag))
        {
            parentBag.Hide();
            return;
        }

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

    // Raises the same event OpenUi does after its attempt checks. Those checks (range, the storage limit, interaction)
    // exist to stop players acting, and the limit would refuse nested bags, which live play bypasses internally.
    private void ForceOpen(EntityUid target, Enum key, EntityUid observer)
    {
        var open = new OpenBoundInterfaceMessage
        {
            Actor = observer,
            Entity = GetNetEntity(target),
            UiKey = key,
        };

        RaiseLocalEvent(target, (object) open, true);
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

            ForceOpen(target, key, observer);

            if (!_ui.IsUiOpen(target, key, observer))
                _viewing.Remove((target, key));
        }
    }
}
