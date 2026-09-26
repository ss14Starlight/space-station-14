using Content.Server._Starlight.Plumbing.Components;
using Content.Server._Starlight.Plumbing.Nodes;
using Content.Server.Popups;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.NodeContainer;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using SharedAppearanceSystem = Robust.Shared.GameObjects.SharedAppearanceSystem;

namespace Content.Server._Starlight.Plumbing.EntitySystems;

/// <summary>
///     Handles inline plumbing filter behavior, reusing the filter control UI.
/// </summary>
[UsedImplicitly]
public sealed partial class PlumbingInlineFilterSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private PlumbingPullSystem _pullSystem = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlumbingInlineFilterComponent, PlumbingDeviceUpdateEvent>(OnDeviceUpdate);
        SubscribeLocalEvent<PlumbingInlineFilterComponent, PlumbingFilterToggleMessage>(OnToggle);
        SubscribeLocalEvent<PlumbingInlineFilterComponent, PlumbingFilterAddReagentMessage>(OnAddReagent);
        SubscribeLocalEvent<PlumbingInlineFilterComponent, PlumbingFilterRemoveReagentMessage>(OnRemoveReagent);
        SubscribeLocalEvent<PlumbingInlineFilterComponent, PlumbingFilterClearMessage>(OnClear);
        SubscribeLocalEvent<PlumbingInlineFilterComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    private void OnDeviceUpdate(Entity<PlumbingInlineFilterComponent> ent, ref PlumbingDeviceUpdateEvent args)
    {
        if (!ent.Comp.Enabled || ent.Comp.FilteredReagents.Count == 0)
        {
            SetRunning(ent, false);
            return;
        }

        if (!TryComp<PlumbingInletComponent>(ent.Owner, out var inlet))
            return;

        if (!_solutionSystem.TryGetSolution(ent.Owner, inlet.SolutionName, out var solutionEnt, out var solution))
            return;

        if (solution.AvailableVolume <= 0)
        {
            SetRunning(ent, false);
            return;
        }

        if (!TryComp<NodeContainerComponent>(ent.Owner, out var nodeContainer))
            return;

        // round robin grabbing of reagents
        var order = BuildRequestOrder(ent.Comp);
        var remaining = inlet.TransferAmount;
        var totalPulled = FixedPoint2.Zero;

        foreach (var inletName in inlet.InletNames)
        {
            if (remaining <= 0 || solution.AvailableVolume <= 0)
                break;

            if (!nodeContainer.Nodes.TryGetValue(inletName, out var node))
                continue;

            if (node is not PlumbingNode plumbingNode || plumbingNode.PlumbingNet == null)
                continue;

            // 10u buffer for each filtered reagent
            var requests = new Dictionary<string, FixedPoint2>();
            foreach (var reagent in order)
            {
                var needed = ent.Comp.ReagentCapacity - solution.GetReagentQuantity(new ReagentId(reagent, null));
                if (needed > 0)
                    requests[reagent] = needed;
            }

            if (requests.Count == 0)
                break;

            var pulled = _pullSystem.PullSpecificReagents(
                ent.Owner,
                plumbingNode.PlumbingNet,
                solutionEnt.Value,
                requests,
                remaining);

            foreach (var amount in pulled.Values)
            {
                remaining -= amount;
                totalPulled += amount;
            }
        }

        // Animation bool
        SetRunning(ent, totalPulled > 0);
    }

    private void SetRunning(Entity<PlumbingInlineFilterComponent> ent, bool running)
        => _appearance.SetData(ent.Owner, PlumbingVisuals.Running, running);

    /// <summary>
    ///     Returns the filtered reagents, rotated by one each update.
    /// </summary>
    private static List<string> BuildRequestOrder(PlumbingInlineFilterComponent comp)
    {
        var order = new List<string>(comp.FilteredReagents.Count);
        foreach (var protoId in comp.FilteredReagents)
        {
            order.Add(protoId.Id);
        }

        // FilteredReagents is a set, so sort for a deterministic base order before rotating.
        order.Sort(StringComparer.Ordinal);

        var offset = comp.ReagentRoundRobinIndex % order.Count;
        comp.ReagentRoundRobinIndex = (offset + 1) % order.Count;

        if (offset == 0)
            return order;

        var rotated = new List<string>(order.Count);
        for (var i = 0; i < order.Count; i++)
        {
            rotated.Add(order[(offset + i) % order.Count]);
        }

        return rotated;
    }

    private void OnToggle(Entity<PlumbingInlineFilterComponent> ent, ref PlumbingFilterToggleMessage args)
    {
        ent.Comp.Enabled = args.Enabled;
        DirtyField(ent, ent.Comp, nameof(PlumbingInlineFilterComponent.Enabled));
        ClickSound(ent.Owner);
        UpdateUI(ent);

        if (!args.Enabled)
            SetRunning(ent, false);
    }

    private void OnAddReagent(Entity<PlumbingInlineFilterComponent> ent, ref PlumbingFilterAddReagentMessage args)
    {
        if (!_prototypeManager.HasIndex<ReagentPrototype>(args.ReagentId))
        {
            _popup.PopupEntity(Loc.GetString("plumbing-filter-invalid-reagent", ("reagent", args.ReagentId)), ent.Owner, args.Actor);
            return;
        }

        var reagentProtoId = new ProtoId<ReagentPrototype>(args.ReagentId);

        if (!ent.Comp.FilteredReagents.Contains(reagentProtoId)
            && ent.Comp.FilteredReagents.Count >= PlumbingInlineFilterComponent.MaxFilteredReagents)
        {
            _popup.PopupEntity(
                Loc.GetString("plumbing-filter-max-reagents", ("count", PlumbingInlineFilterComponent.MaxFilteredReagents)),
                ent.Owner,
                args.Actor);
            return;
        }

        ent.Comp.FilteredReagents.Add(reagentProtoId);
        DirtyField(ent, ent.Comp, nameof(PlumbingInlineFilterComponent.FilteredReagents));
        ClickSound(ent.Owner);
        UpdateUI(ent);
    }

    private void OnRemoveReagent(Entity<PlumbingInlineFilterComponent> ent, ref PlumbingFilterRemoveReagentMessage args)
    {
        ent.Comp.FilteredReagents.Remove(new ProtoId<ReagentPrototype>(args.ReagentId));
        DirtyField(ent, ent.Comp, nameof(PlumbingInlineFilterComponent.FilteredReagents));
        ClickSound(ent.Owner);
        UpdateUI(ent);
    }

    private void OnClear(Entity<PlumbingInlineFilterComponent> ent, ref PlumbingFilterClearMessage args)
    {
        ent.Comp.FilteredReagents.Clear();
        DirtyField(ent, ent.Comp, nameof(PlumbingInlineFilterComponent.FilteredReagents));
        ClickSound(ent.Owner);
        UpdateUI(ent);
    }

    private void OnUIOpened(Entity<PlumbingInlineFilterComponent> ent, ref BoundUIOpenedEvent args)
        => UpdateUI(ent);

    private void UpdateUI(Entity<PlumbingInlineFilterComponent> ent)
    {
        // Convert ProtoId to string for UI state
        var filteredReagents = new HashSet<string>();
        foreach (var protoId in ent.Comp.FilteredReagents)
        {
            filteredReagents.Add(protoId.Id);
        }

        var state = new PlumbingFilterBoundUserInterfaceState(
            filteredReagents,
            ent.Comp.Enabled);

        _ui.SetUiState(ent.Owner, PlumbingFilterUiKey.Key, state);
    }

    private void ClickSound(EntityUid uid)
    {
        if (TryComp<PlumbingDeviceComponent>(uid, out var device))
            _audio.PlayPvs(device.ClickSound, uid, AudioParams.Default.WithVolume(-2f));
    }
}
