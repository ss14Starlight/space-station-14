using Content.Server._Starlight.Plumbing.Components;
using Content.Server._Starlight.Plumbing.Nodes;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Popups;
using Content.Server.UserInterface;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.NodeContainer;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Plumbing.EntitySystems;

/// <summary>
///     Handles plumbing filter behavior and filter control UI.
///     Intake routing into filtered/passthrough lanes is handled here on device update.
///     Outlets still enforce reagent restrictions by node:
///     - Filter outlet: only allows pulling reagents matching the filter list
///     - Passthrough outlet: only allows pulling reagents NOT matching the filter list
///     Restriction is enforced via PlumbingPullAttemptEvent.
/// </summary>
[UsedImplicitly]
public sealed class PlumbingFilterSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly PlumbingPullSystem _pullSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlumbingFilterComponent, PlumbingPullAttemptEvent>(OnPullAttempt);
    SubscribeLocalEvent<PlumbingFilterComponent, PlumbingDeviceUpdateEvent>(OnDeviceUpdate);
        SubscribeLocalEvent<PlumbingFilterComponent, PlumbingFilterToggleMessage>(OnToggle);
        SubscribeLocalEvent<PlumbingFilterComponent, PlumbingFilterAddReagentMessage>(OnAddReagent);
        SubscribeLocalEvent<PlumbingFilterComponent, PlumbingFilterRemoveReagentMessage>(OnRemoveReagent);
        SubscribeLocalEvent<PlumbingFilterComponent, PlumbingFilterClearMessage>(OnClear);
        SubscribeLocalEvent<PlumbingFilterComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    /// <summary>
    ///     Handles pull attempts - restricts which reagents can be pulled based on outlet node.
    /// </summary>
    private void OnPullAttempt(Entity<PlumbingFilterComponent> ent, ref PlumbingPullAttemptEvent args)
    {
        // When disabled, block the filter outlet entirely — everything goes through passthrough
        if (!ent.Comp.Enabled)
        {
            if (args.NodeName == ent.Comp.FilterNodeName)
                args.Cancelled = true;
            return;
        }

        var isFilteredReagent = ent.Comp.FilteredReagents.Contains(args.ReagentPrototype);

        if (args.NodeName == ent.Comp.FilterNodeName)
        {
            if (!isFilteredReagent)
                args.Cancelled = true;
        }
        else if (args.NodeName == ent.Comp.PassthroughNodeName)
        {
            if (isFilteredReagent)
                args.Cancelled = true;
        }
    }

    private void OnDeviceUpdate(Entity<PlumbingFilterComponent> ent, ref PlumbingDeviceUpdateEvent args)
    {
        if (!TryComp<PlumbingInletComponent>(ent.Owner, out var inlet))
            return;

        if (!_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.FilteredSolutionName, out var filteredEnt, out var filteredSolution))
            return;

        if (!_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.PassthroughSolutionName, out var passthroughEnt, out var passthroughSolution))
            return;

        if (filteredSolution.AvailableVolume <= 0 && passthroughSolution.AvailableVolume <= 0)
            return;

        if (!TryComp<NodeContainerComponent>(ent.Owner, out var nodeContainer))
            return;

        var remaining = inlet.TransferAmount;

        foreach (var inletName in inlet.InletNames)
        {
            if (remaining <= 0)
                break;

            if (filteredSolution.AvailableVolume <= 0 && passthroughSolution.AvailableVolume <= 0)
                break;

            if (!nodeContainer.Nodes.TryGetValue(inletName, out var node))
                continue;

            if (node is not PlumbingNode plumbingNode || plumbingNode.PlumbingNet == null)
                continue;

            var roundRobinIndex = inlet.RoundRobinIndices.GetValueOrDefault(inletName, 0);
            var (pulled, nextIndex) = _pullSystem.PullFromNetworkSplit(
                ent.Owner,
                plumbingNode.PlumbingNet,
                filteredEnt.Value,
                passthroughEnt.Value,
                remaining,
                roundRobinIndex,
                ent.Comp.Enabled,
                ent.Comp.FilteredReagents);

            inlet.RoundRobinIndices[inletName] = nextIndex;
            remaining -= pulled;
        }
    }

    private void OnToggle(Entity<PlumbingFilterComponent> ent, ref PlumbingFilterToggleMessage args)
    {
        ent.Comp.Enabled = args.Enabled;
        DirtyField(ent, ent.Comp, nameof(PlumbingFilterComponent.Enabled));
        ClickSound(ent.Owner);
        UpdateUI(ent);
    }

    private void OnAddReagent(Entity<PlumbingFilterComponent> ent, ref PlumbingFilterAddReagentMessage args)
    {
        if (!_prototypeManager.HasIndex<ReagentPrototype>(args.ReagentId))
        {
            _popup.PopupEntity(Loc.GetString("plumbing-filter-invalid-reagent", ("reagent", args.ReagentId)), ent.Owner, args.Actor);
            return;
        }

        var reagentProtoId = new ProtoId<ReagentPrototype>(args.ReagentId);

        if (!ent.Comp.FilteredReagents.Contains(reagentProtoId)
            && ent.Comp.FilteredReagents.Count >= PlumbingFilterComponent.MaxFilteredReagents)
        {
            _popup.PopupEntity(
                Loc.GetString("plumbing-filter-max-reagents", ("count", PlumbingFilterComponent.MaxFilteredReagents)),
                ent.Owner,
                args.Actor);
            return;
        }

        ent.Comp.FilteredReagents.Add(reagentProtoId);
        DirtyField(ent, ent.Comp, nameof(PlumbingFilterComponent.FilteredReagents));
        ClickSound(ent.Owner);
        UpdateUI(ent);
    }

    private void OnRemoveReagent(Entity<PlumbingFilterComponent> ent, ref PlumbingFilterRemoveReagentMessage args)
    {
        ent.Comp.FilteredReagents.Remove(new ProtoId<ReagentPrototype>(args.ReagentId));
        DirtyField(ent, ent.Comp, nameof(PlumbingFilterComponent.FilteredReagents));
        ClickSound(ent.Owner);
        UpdateUI(ent);
    }

    private void OnClear(Entity<PlumbingFilterComponent> ent, ref PlumbingFilterClearMessage args)
    {
        ent.Comp.FilteredReagents.Clear();
        DirtyField(ent, ent.Comp, nameof(PlumbingFilterComponent.FilteredReagents));
        ClickSound(ent.Owner);
        UpdateUI(ent);
    }

    private void OnUIOpened(Entity<PlumbingFilterComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUI(ent);
    }

    private void UpdateUI(Entity<PlumbingFilterComponent> ent)
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
