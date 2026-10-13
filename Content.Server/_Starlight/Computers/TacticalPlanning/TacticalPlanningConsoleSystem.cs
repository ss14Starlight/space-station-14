using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Popups;
using Content.Server.Station.Components;
using Content.Shared._Starlight.Computers.TacticalPlanning;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Computers.TacticalPlanning;

/// <summary>
/// Keeps annotations server-side and sends snapshots/deltas only to viewers of this console.
/// </summary>
public sealed partial class TacticalPlanningConsoleSystem : EntitySystem
{
    #region Variables

    private const int MaxViewers = 16;
    private static readonly TimeSpan SubmissionInterval = TimeSpan.FromMilliseconds(200);

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    #endregion

    #region Methods

    /// <summary>Registers the console's bounded UI operations.</summary>
    public override void Initialize()
    {
        SubscribeLocalEvent<TacticalPlanningConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TacticalPlanningConsoleComponent, GotEmaggedEvent>(OnEmagged);
        Subs.BuiEvents<TacticalPlanningConsoleComponent>(TacticalPlanningConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<BoundUIClosedEvent>(OnClosed);
            subs.Event<TacticalPlanRequestSnapshotMessage>(OnRequestSnapshot);
            subs.Event<TacticalPlanAddAnnotationMessage>(OnAddAnnotation);
            subs.Event<TacticalPlanEraseAnnotationMessage>(OnEraseAnnotation);
            subs.Event<TacticalPlanResetMessage>(OnReset);
        });
    }

    /// <summary>Unlocks planning symbols only on the Syndicate variant, without changing ordinary consoles.</summary>
    private void OnEmagged(Entity<TacticalPlanningConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (!HasComp<NukeopsStationMapComponent>(ent)
            || !_emag.CompareFlag(args.Type, EmagType.Interaction)
            || _emag.CheckFlag(ent, EmagType.Interaction))
            return;

        args.Handled = true;
        // EmagSystem records the flag after this event returns; notify existing viewers now.
        SendToViewers(ent.Owner, ent.Comp, new TacticalPlanIconsUnlockedMessage());
    }

    /// <summary>Sets the initial map target without changing upstream station-map initialization.</summary>
    private void OnMapInit(Entity<TacticalPlanningConsoleComponent> ent, ref MapInitEvent args)
    {
        UpdateTarget(ent);
    }

    /// <summary>Targets the console's current grid, or the nukies' station target when applicable.</summary>
    private void UpdateTarget(Entity<TacticalPlanningConsoleComponent> ent)
    {
        if (!TryComp<StationMapComponent>(ent, out var map))
            return;

        // Refresh on snapshot as well as map init: consoles can move, and nukie rules can start later.
        var grid = HasComp<NukeopsStationMapComponent>(ent)
            ? GetNukeopsTargetGrid()
            : Transform(ent).GridUid;
        if (map.TargetGrid == grid)
            return;

        map.TargetGrid = grid;
        Dirty(ent.Owner, map);
    }

    /// <summary>Uses an active nukie target, otherwise the main station grid on the round's map.</summary>
    private EntityUid? GetNukeopsTargetGrid()
    {
        foreach (var rule in _gameTicker.GetActiveGameRules())
        {
            if (TryComp<NukeopsRuleComponent>(rule, out var nukeopsRule) && nukeopsRule.TargetStation != null)
                return _station.GetLargestGrid((nukeopsRule.TargetStation.Value, null));
        }

        // MainGrids excludes auxiliary station grids; DefaultMap excludes off-station bases.
        // Break equal-size ties by grid UID so station query order cannot affect the fallback.
        EntityUid? fallbackGrid = null;
        var largestMainGrid = -1f;
        foreach (var station in _station.GetStations())
        {
            if (!HasComp<StationEventEligibleComponent>(station)
                || !TryComp<StationDataComponent>(station, out var data))
                continue;

            foreach (var mainGrid in data.MainGrids)
            {
                if (Transform(mainGrid).MapID != _gameTicker.DefaultMap
                    || !TryComp<MapGridComponent>(mainGrid, out var mainGridComp))
                    continue;

                var size = mainGridComp.LocalAABB.Size.LengthSquared();
                if (size < largestMainGrid
                    || (size == largestMainGrid && fallbackGrid != null && mainGrid.CompareTo(fallbackGrid.Value) >= 0))
                    continue;

                largestMainGrid = size;
                fallbackGrid = mainGrid;
            }
        }

        return fallbackGrid;
    }

    /// <summary>Registers a newly opened viewer for targeted plan updates.</summary>
    private void OnOpened(EntityUid uid, TacticalPlanningConsoleComponent component, BoundUIOpenedEvent args)
    {
        TryRegisterViewer(uid, component, args.Actor);
    }

    /// <summary>Removes a viewer without letting a quick reopen bypass the edit interval.</summary>
    private void OnClosed(EntityUid uid, TacticalPlanningConsoleComponent component, BoundUIClosedEvent args)
    {
        component.Viewers.Remove(args.Actor);
        // A quick reopen must not bypass the per-player edit interval.
        if (component.LastSubmission.TryGetValue(args.Actor, out var last)
            && _timing.CurTime - last >= SubmissionInterval)
            component.LastSubmission.Remove(args.Actor);
        component.LastRejectionNotice.Remove(args.Actor);
    }

    /// <summary>Sends a full plan or only changes missing from the client's cache.</summary>
    private void OnRequestSnapshot(EntityUid uid, TacticalPlanningConsoleComponent component, TacticalPlanRequestSnapshotMessage args)
    {
        if (args.Actor is not { Valid: true } actor || !TryRegisterViewer(uid, component, actor))
            return;

        // Recheck without polling: nukie rules may start late, and a regular console may move grids.
        UpdateTarget((uid, component));
        var color = component.PlayerColors[actor];

        // A previously opened client only needs the difference from the current plan.
        // Never trust an unbounded list supplied by a client.
        var full = args.CacheToken != component.CacheToken || args.KnownIds == null
            || args.KnownIds.Count > component.MaxAnnotations;
        var known = full ? null : new HashSet<int>(args.KnownIds!);
        var annotations = new List<TacticalPlanAnnotation>();
        var removed = new List<int>();
        if (full)
        {
            annotations.AddRange(component.Annotations);
        }
        else
        {
            foreach (var annotation in component.Annotations)
            {
                if (!known!.Remove(annotation.Id))
                    annotations.Add(annotation);
            }

            removed.AddRange(known!);
        }

        _ui.ServerSendUiMessage(uid, TacticalPlanningConsoleUiKey.Key,
            new TacticalPlanSnapshotMessage(component.CacheToken, full, annotations, removed, color,
                IsFull(component), HasComp<NukeopsStationMapComponent>(uid), IconsUnlocked(uid)), actor);
    }

    /// <summary>Validates and stores a drawing, then sends it only to current viewers.</summary>
    private void OnAddAnnotation(EntityUid uid, TacticalPlanningConsoleComponent component, TacticalPlanAddAnnotationMessage args)
    {
        if (args.Actor is not { Valid: true } actor || !TryRegisterViewer(uid, component, actor))
            return;

        // Picker movement stays client-side; validate and save its color only with an accepted drawing.
        if (!TacticalPlanDrawingValidation.TryValidate(args.Kind, args.Points, args.Color, out var color, args.Icon)
            || (args.Kind == TacticalPlanAnnotationKind.Icon && !IconsUnlocked(uid)))
            return;

        if (IsFull(component) || component.TotalPoints + args.Points.Count > component.MaxTotalPoints)
        {
            if (!IsFull(component))
            {
                // A stroke may not fit in the remaining point budget even before it reaches
                // exactly zero. Treat that rejection as full so viewers can reset the plan.
                component.CapacityReached = true;
                SendToViewers(uid, component, new TacticalPlanCapacityReachedMessage());
            }

            RejectDrawing(uid, component, actor, Loc.GetString("tactical-planning-console-plan-full-rejection"));
            return;
        }

        if (!CanSubmit(component, actor))
        {
            RejectDrawing(uid, component, actor, Loc.GetString("tactical-planning-console-rate-limit-rejection"));
            return;
        }

        if (args.Kind != TacticalPlanAnnotationKind.Icon)
            component.PlayerColors[actor] = color;
        var annotation = new TacticalPlanAnnotation(component.NextAnnotationId++, args.Kind, color,
            new List<Vector2>(args.Points), MetaData(actor).EntityName, args.Icon);
        component.Annotations.Add(annotation);
        component.TotalPoints += annotation.Points.Count;
        SendToViewers(uid, component, new TacticalPlanAnnotationAddedMessage(annotation, IsFull(component)));
    }

    /// <summary>Erases an existing annotation under the same edit limits as drawing.</summary>
    private void OnEraseAnnotation(EntityUid uid, TacticalPlanningConsoleComponent component, TacticalPlanEraseAnnotationMessage args)
    {
        if (args.Actor is not { Valid: true } actor || !TryRegisterViewer(uid, component, actor))
            return;

        var index = component.Annotations.FindIndex(annotation => annotation.Id == args.Id);
        if (index < 0)
            return;

        if (!CanSubmit(component, actor))
            return;

        component.TotalPoints -= component.Annotations[index].Points.Count;
        component.Annotations.RemoveAt(index);
        component.CapacityReached = false;
        SendToViewers(uid, component, new TacticalPlanAnnotationErasedMessage(args.Id, IsFull(component)));
    }

    /// <summary>Clears a full plan in one event while retaining its server-side audit record.</summary>
    private void OnReset(EntityUid uid, TacticalPlanningConsoleComponent component, TacticalPlanResetMessage args)
    {
        if (args.Actor is not { Valid: true } actor || !TryRegisterViewer(uid, component, actor)
            || !IsFull(component))
            return;

        var count = component.Annotations.Count;
        component.Annotations.Clear();
        component.TotalPoints = 0;
        component.CapacityReached = false;
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(actor):player} reset {ToPrettyString(uid):entity}'s tactical plan, clearing {count} annotations.");
        SendToViewers(uid, component, new TacticalPlanClearedMessage());
    }

    /// <summary>Reports whether either annotation capacity has been reached.</summary>
    private static bool IsFull(TacticalPlanningConsoleComponent component) =>
        component.CapacityReached || component.Annotations.Count >= component.MaxAnnotations
            || component.TotalPoints >= component.MaxTotalPoints;

    /// <summary>Checks the server-side emag flag; the client never grants itself icon placement.</summary>
    private bool IconsUnlocked(EntityUid uid) =>
        HasComp<NukeopsStationMapComponent>(uid) && _emag.CheckFlag(uid, EmagType.Interaction);

    /// <summary>Admits an open-UI actor without exceeding the per-console viewer cap.</summary>
    private bool TryRegisterViewer(EntityUid uid, TacticalPlanningConsoleComponent component, EntityUid actor)
    {
        // The engine's open UI state is the authority. A missed snapshot must not leave an
        // otherwise valid viewer permanently unable to submit annotations.
        if (!_ui.IsUiOpen(uid, TacticalPlanningConsoleUiKey.Key, actor))
            return false;

        component.Viewers.RemoveWhere(viewer => !_ui.IsUiOpen(uid, TacticalPlanningConsoleUiKey.Key, viewer));
        if (!component.Viewers.Contains(actor) && component.Viewers.Count >= MaxViewers)
        {
            _popup.PopupEntity(Loc.GetString("tactical-planning-console-viewer-capacity"), uid, actor);
            _ui.CloseUi(uid, TacticalPlanningConsoleUiKey.Key, actor);
            return false;
        }

        component.Viewers.Add(actor);
        if (!component.PlayerColors.ContainsKey(actor))
            component.PlayerColors[actor] = Color.FromHsv(new Vector4(_random.NextFloat(), 0.8f, 0.9f, 1f));

        return true;
    }

    /// <summary>Returns a throttled rejection notice without showing unaccepted ink.</summary>
    private void RejectDrawing(EntityUid uid, TacticalPlanningConsoleComponent component, EntityUid actor, string reason)
    {
        // Rejections are user-visible, but should not themselves become a message flood.
        var now = _timing.CurTime;
        if (component.LastRejectionNotice.TryGetValue(actor, out var last) && now - last < TimeSpan.FromSeconds(1))
            return;

        component.LastRejectionNotice[actor] = now;
        _ui.ServerSendUiMessage(uid, TacticalPlanningConsoleUiKey.Key,
            new TacticalPlanDrawingRejectedMessage(reason), actor);
    }

    /// <summary>Enforces per-player timing and the console-wide submission budget.</summary>
    private bool CanSubmit(TacticalPlanningConsoleComponent component, EntityUid actor)
    {
        // Bound each player to one edit per 200 ms, then share a 12-edit/second token
        // bucket across this console. Both budgets update only when an edit is attempted.
        var now = _timing.CurTime;
        if (component.LastSubmission.TryGetValue(actor, out var last) && now - last < SubmissionInterval)
            return false;

        // A small burst is fine, but one console cannot produce an unbounded stream of deltas.
        var elapsed = Math.Max(0d, (now - component.LastBudgetUpdate).TotalSeconds);
        component.SubmissionTokens = MathF.Min(5f, component.SubmissionTokens + (float) elapsed * 12f);
        component.LastBudgetUpdate = now;
        if (component.SubmissionTokens < 1f)
            return false;

        component.SubmissionTokens -= 1f;
        component.LastSubmission[actor] = now;
        return true;
    }

    /// <summary>Broadcasts a small plan delta only to users with this UI open.</summary>
    private void SendToViewers(EntityUid uid, TacticalPlanningConsoleComponent component, BoundUserInterfaceMessage message)
    {
        component.Viewers.RemoveWhere(viewer => !_ui.IsUiOpen(uid, TacticalPlanningConsoleUiKey.Key, viewer));
        foreach (var viewer in component.Viewers)
            _ui.ServerSendUiMessage(uid, TacticalPlanningConsoleUiKey.Key, message, viewer);
    }

    #endregion
}
