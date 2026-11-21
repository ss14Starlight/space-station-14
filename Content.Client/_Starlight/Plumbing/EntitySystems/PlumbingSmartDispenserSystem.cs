using Content.Client._Starlight.Plumbing.Components;
using Content.Client._Starlight.Plumbing.UI;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared._Starlight.Plumbing.EntitySystems;
using Content.Shared.Labels.Components;
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

    [SubscribeNetworkEvent]
    private void OnRemoteBuiOpenedByRemoteActor(RemoteControlBuiOpenedByRemoteActorEvent args)
    {
        if (args.UiKey != RemoteControlBuiOpenedByRemoteActorEvent.GetUiKeyIdentifier(PlumbingSmartDispenserUiKey.Key)
            || !TryGetEntity(args.UiEntity, out var dispenser)
            || dispenser is not { Valid: true } dispenserUid
            || !TryComp<PlumbingSmartDispenserComponent>(dispenserUid, out var component))
            return;

        EntityUid? outputContainer = null;
        if (TryGetEntity(args.RemoteActor, out var remoteActor)
            && remoteActor is { Valid: true } actor
            && _hands.TryGetActiveItem(actor, out var activeItem)
            && activeItem is { Valid: true } item
            && (HasComp<HandLabelerComponent>(item) || GetFillSolutionFromContainer(item) is not null))
            outputContainer = item;

        EnsureComp<PlumbingSmartDispenserLocalStateComponent>(dispenserUid).OutputContainerEntity = outputContainer;
        UpdateUi(new Entity<PlumbingSmartDispenserComponent>(dispenserUid, component));
    }

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
