using Content.Client._Starlight.Plumbing.Components;
using Content.Client._Starlight.Plumbing.UI;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared._Starlight.Plumbing.EntitySystems;
using Robust.Client.Timing;

namespace Content.Client._Starlight.Plumbing.EntitySystems;

public sealed partial class PlumbingSmartDispenserSystem : SharedPlumbingSmartDispenserSystem
{
    [Dependency] private IClientGameTiming _timing = null!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<PlumbingSmartDispenserComponent>(PlumbingSmartDispenserUiKey.Key, subs => {
            subs.Event<PlumbingSmartDispenserSetDispenseAmountMessage>(OnSetDispenseAmountMessage);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
        });
    }

    [SubscribeLocalEvent]
    private void OnAutoState(Entity<PlumbingSmartDispenserComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateUi(ent);

    private void OnUiClosed(Entity<PlumbingSmartDispenserComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!_timing.IsFirstTimePredicted) return;

        if (TryComp<PlumbingSmartDispenserLocalStateComponent>(ent.Owner, out var localState))
            localState.OutputContainerEntity = null;
    }

    private void OnSetDispenseAmountMessage(Entity<PlumbingSmartDispenserComponent> ent, ref PlumbingSmartDispenserSetDispenseAmountMessage args)
    {
        var localState = EnsureComp<PlumbingSmartDispenserLocalStateComponent>(ent.Owner);
        localState.SelectedDispenseAmount = args.DispenseAmount;
        UpdateUi(ent);
    }

    protected override void SetInteractedWith(Entity<PlumbingSmartDispenserComponent> ent, EntityUid? container)
    {
        if (!_timing.IsFirstTimePredicted) return;
        var localState = EnsureComp<PlumbingSmartDispenserLocalStateComponent>(ent.Owner);
        localState.OutputContainerEntity = container;
        UpdateUi(ent);
    }

    protected override void UpdateUi(Entity<PlumbingSmartDispenserComponent> ent)
    {
        if (_uiSystem.TryGetOpenUi(ent.Owner, PlumbingSmartDispenserUiKey.Key, out PlumbingSmartDispenserBoundUserInterface? bui))
            bui.Update();
    }
}
