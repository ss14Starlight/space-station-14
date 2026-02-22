using Content.Server._Starlight.Plumbing.Components;
using Content.Server.Popups;
using Content.Server.UserInterface;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Plumbing.EntitySystems;

/// <summary>
///     Handles plumbing filter behavior - pulls from inlet into a single buffer container.
///     The buffer has two outlet nodes with restricted pulling:
///     - Filter outlet: only allows pulling reagents matching the filter list
///     - Passthrough outlet: only allows pulling reagents NOT matching the filter list
///     Restriction is enforced via PlumbingPullAttemptEvent.
/// </summary>
[UsedImplicitly]
public sealed class PlumbingFilterSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlumbingFilterComponent, PlumbingPullAttemptEvent>(OnPullAttempt);
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

        ent.Comp.FilteredReagents.Add(new ProtoId<ReagentPrototype>(args.ReagentId));
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
