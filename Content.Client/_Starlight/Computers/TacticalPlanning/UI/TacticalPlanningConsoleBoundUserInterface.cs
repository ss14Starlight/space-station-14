using System.Numerics;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Content.Shared._Starlight.Computers.TacticalPlanning;
using Content.Shared.Pinpointer;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Computers.TacticalPlanning.UI;

/// <summary>
/// Reconciles the server's shared plan with a small client cache and feeds the window.
/// Drawing remains disabled until the server confirms the current snapshot.
/// </summary>
public sealed class TacticalPlanningConsoleBoundUserInterface : BoundUserInterface
{
    // This cache must outlive each Bound UI instance so reopening requests only plan changes.
    // The server token prevents a reused entity ID from reviving another round's annotations.
    private static readonly Dictionary<NetEntity, PlanCache> Cache = new();
    private const int MaxCachedConsoles = 16;
    private const int MaxSnapshotAttempts = 4;
    private static readonly TimeSpan SnapshotRetryInterval = TimeSpan.FromSeconds(1);

    private sealed class PlanCache(string token)
    {
        public readonly string Token = token;
        public readonly List<TacticalPlanAnnotation> Annotations = new();
        public Color? SelectedColor;

        /// <summary>Merges a server annotation into this console's local cache.</summary>
        public void Add(TacticalPlanAnnotation annotation)
        {
            if (!Annotations.Exists(existing => existing.Id == annotation.Id))
                Annotations.Add(annotation);
        }
    }

    private TacticalPlanningConsoleWindow? _window;
    private NetEntity _cacheKey;
    private PlanCache? _cache;
    private readonly List<BoundUserInterfaceMessage> _pending = new();
    private bool _synchronizing;
    private int _snapshotAttempts;
    private CancellationTokenSource? _snapshotRetryCancellation;
    private EntityUid? _displayedGrid;
    private bool _mapInitialized;

    public TacticalPlanningConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<TacticalPlanningConsoleWindow>();
        var metadata = EntMan.GetComponent<MetaDataComponent>(Owner);
        _window.Title = metadata.EntityName;
        // Set the initial tint before the map renders; the server snapshot confirms it later.
        _window.SetSyndicatePalette(EntMan.HasComponent<NukeopsStationMapComponent>(Owner));
        _window.AnnotationAdded += (kind, points) =>
        {
            if (_synchronizing || _window == null
                || !TacticalPlanDrawingValidation.TryValidate(kind, points, _window.InkColor, out var color))
                return;

            // Keep a rejection visible until this player tries another drawing.
            _window.SetConnectionStatus(null, true);
            SendMessage(new TacticalPlanAddAnnotationMessage(kind, points, color));
        };
        _window.IconAdded += (position, icon) =>
        {
            var points = new List<Vector2> { position };
            if (_synchronizing || _window == null
                || !TacticalPlanDrawingValidation.TryValidate(TacticalPlanAnnotationKind.Icon,
                    points, Color.White, out var color, icon))
                return;

            _window.SetConnectionStatus(null, true);
            SendMessage(new TacticalPlanAddAnnotationMessage(TacticalPlanAnnotationKind.Icon, points, color, icon));
        };
        _window.AnnotationErased += id =>
        {
            if (!_synchronizing)
                SendMessage(new TacticalPlanEraseAnnotationMessage(id));
        };
        // Picker movement is local. Only a completed drawing sends its color to the server.
        _window.ColorSelected += color => _cache?.SelectedColor = color;
        _window.PlanReset += () =>
        {
            if (!_synchronizing)
                SendMessage(new TacticalPlanResetMessage());
        };

        RefreshMapTarget();
        _synchronizing = true;
        _snapshotAttempts = 0;
        _snapshotRetryCancellation = new CancellationTokenSource();
        _window.SetConnectionStatus(Loc.GetString("tactical-planning-console-connecting"), false);
        _cacheKey = EntMan.GetNetEntity(Owner);
        Cache.TryGetValue(_cacheKey, out _cache);
        if (_cache != null)
        {
            _window.SetAnnotations(_cache.Annotations);
            if (_cache.SelectedColor is { } color)
                _window.SetInk(color);
        }
        RequestSnapshot();
    }

    /// <summary>Follows changes to the console's target grid while its UI is open.</summary>
    public override void Update()
    {
        base.Update();
        RefreshMapTarget();
    }

    private void RequestSnapshot()
    {
        // A missed opening message must not strand the UI in "Connecting". This one-shot
        // retry chain runs only while the window is open, rather than polling every frame.
        _snapshotAttempts++;
        SendMessage(new TacticalPlanRequestSnapshotMessage(_cache?.Token ?? string.Empty,
            _cache?.Annotations.ConvertAll(annotation => annotation.Id) ?? new List<int>()));
        if (_snapshotRetryCancellation == null)
            return;

        Timer.Spawn(SnapshotRetryInterval, () =>
        {
            if (!_synchronizing || !IsOpened)
                return;

            if (_snapshotAttempts >= MaxSnapshotAttempts)
                _window?.SetConnectionStatus(Loc.GetString("tactical-planning-console-sync-failed"), false);
            else
                RequestSnapshot();
        }, _snapshotRetryCancellation.Token);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _snapshotRetryCancellation?.Cancel();
            _snapshotRetryCancellation?.Dispose();
            _snapshotRetryCancellation = null;
        }

        base.Dispose(disposing);
    }

    private void RefreshMapTarget()
    {
        if (_window == null)
            return;

        EntityUid? gridUid = null;
        if (EntMan.TryGetComponent<StationMapComponent>(Owner, out var map) && map.TargetGrid != null)
            gridUid = map.TargetGrid;

        if (_mapInitialized && gridUid == _displayedGrid)
            return;

        _mapInitialized = true;
        _displayedGrid = gridUid;
        var stationName = gridUid != null && EntMan.TryGetComponent<MetaDataComponent>(gridUid, out var meta)
            ? meta.EntityName
            : Loc.GetString("tactical-planning-console-waiting-for-target");
        _window.Set(stationName, gridUid);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        switch (message)
        {
            case TacticalPlanSnapshotMessage snapshot:
                // Cached drawings are only a display shortcut. The server token and ID diff
                // decide what survives; a new token always replaces the old plan entirely.
                if (snapshot.Full || _cache == null || _cache.Token != snapshot.CacheToken)
                {
                    _cache = new PlanCache(snapshot.CacheToken);
                    if (Cache.Count >= MaxCachedConsoles && !Cache.ContainsKey(_cacheKey))
                        Cache.Clear();
                    Cache[_cacheKey] = _cache;
                }

                if (snapshot.Full)
                    _cache.Annotations.Clear();
                else
                    _cache.Annotations.RemoveAll(annotation => snapshot.RemovedIds.Contains(annotation.Id));

                foreach (var annotation in snapshot.Annotations)
                    _cache.Add(annotation);

                _window?.SetSyndicatePalette(snapshot.SyndicatePalette);
                _window?.SetIconToolAvailable(snapshot.IconsUnlocked);
                _window?.SetAnnotations(_cache.Annotations);
                _window?.SetPlanFull(snapshot.IsFull);
                _window?.SetInk(_cache.SelectedColor ?? snapshot.AssignedColor);
                _synchronizing = false;
                _snapshotRetryCancellation?.Cancel();
                _window?.SetConnectionStatus(null, true);
                // Deltas sent while the snapshot was in flight must be applied afterwards.
                foreach (var pending in _pending)
                    ApplyDelta(pending);
                _pending.Clear();
                break;
            case TacticalPlanDrawingRejectedMessage:
            case TacticalPlanCapacityReachedMessage:
            case TacticalPlanAnnotationAddedMessage:
            case TacticalPlanAnnotationErasedMessage:
            case TacticalPlanClearedMessage:
            case TacticalPlanIconsUnlockedMessage:
                if (_synchronizing)
                {
                    _pending.Add(message);
                    break;
                }

                ApplyDelta(message);
                break;
        }
    }

    private void ApplyDelta(BoundUserInterfaceMessage message)
    {
        if (_cache == null)
            return;

        switch (message)
        {
            case TacticalPlanAnnotationAddedMessage added:
                _cache.Add(added.Annotation);
                _window?.AddAnnotation(added.Annotation);
                _window?.SetPlanFull(added.IsFull);
                break;
            case TacticalPlanDrawingRejectedMessage rejected:
                _window?.SetConnectionStatus(rejected.Reason, !_synchronizing);
                break;
            case TacticalPlanAnnotationErasedMessage erased:
                _cache.Annotations.RemoveAll(annotation => annotation.Id == erased.Id);
                _window?.RemoveAnnotation(erased.Id);
                _window?.SetPlanFull(erased.IsFull);
                break;
            case TacticalPlanClearedMessage:
                _cache.Annotations.Clear();
                _window?.SetAnnotations(_cache.Annotations);
                _window?.SetPlanFull(false);
                break;
            case TacticalPlanCapacityReachedMessage:
                _window?.SetPlanFull(true);
                break;
            case TacticalPlanIconsUnlockedMessage:
                _window?.SetIconToolAvailable(true);
                break;
        }
    }
}
