using Content.Shared.Implants.Components;
using Content.Shared.Popups;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Store.Events;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

#region Starlight
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._Starlight.Store.Components;
#endregion Starlight

namespace Content.Server.Store.Systems;

/// <summary>
/// Manages general interactions with a store and different entities,
/// getting listings for stores, and interfacing with the store UI.
/// </summary>
public sealed partial class StoreSystem : SharedStoreSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StoreComponent, ActivatableUIOpenAttemptEvent>(OnStoreOpenAttempt);
        //SubscribeLocalEvent<AccessReaderComponent, ActivatableUIOpenAttemptEvent>(OnStoreOpenAttempt); //🌟Starlight🌟 This broke all the computers.
        SubscribeLocalEvent<StoreComponent, BeforeActivatableUIOpenEvent>(BeforeActivatableUiOpen);

        SubscribeLocalEvent<StoreComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StoreComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StoreComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RemoteStoreComponent, OpenUplinkImplantEvent>(OnImplantActivate);

        InitializeUi();
        InitializeCommand();
        InitializeRefund();
    }
    //🌟Starlight🌟
    private void OnStoreOpenAttempt(Entity<AccessReaderComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!_accessReader.IsAllowed(args.User, ent, ent.Comp))
            args.Cancel();
    }

    private void OnMapInit(EntityUid uid, StoreComponent component, MapInitEvent args)
    {
        // Starlight-start: Ensure the store has a StockLimitedProcessingComponent
        EnsureComp<StockLimitedProcessingComponent>(uid);

        RefreshAllListings((uid, component));
        // Starlight-end
        component.StartingMap = Transform(uid).MapUid;

        if (!_ui.HasUi(uid, StoreUiKey.Key))
            _ui.SetUi(uid, StoreUiKey.Key, new InterfaceData("StoreBoundUserInterface"));
    }

    private void OnStartup(EntityUid uid, StoreComponent component, ComponentStartup args)
    {
        // for traitors, because the StoreComponent for the PDA can be added at any time.
        if (MetaData(uid).EntityLifeStage == EntityLifeStage.MapInitialized)
        {
            // Starlight-start
            RefreshAllListings((uid, component));
            // Starlight-end
        }

        var ev = new StoreAddedEvent();
        RaiseLocalEvent(uid, ref ev, true);
    }

    private void OnShutdown(EntityUid uid, StoreComponent component, ComponentShutdown args)
    {
        var ev = new StoreRemovedEvent();
        RaiseLocalEvent(uid, ref ev, true);
    }

    private void OnStoreOpenAttempt(EntityUid uid, StoreComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!component.OwnerOnly)
            return;

        if (!_mind.TryGetMind(args.User, out var mind, out _))
            return;

        component.AccountOwner ??= mind;
        DebugTools.Assert(component.AccountOwner != null);

        if (component.AccountOwner == mind)
            return;

        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("store-not-account-owner", ("store", uid)), uid, args.User);

        args.Cancel();
    }

    private void OnImplantActivate(Entity<RemoteStoreComponent> entity, ref OpenUplinkImplantEvent args)
    {
        if (GetRemoteStore(entity.AsNullable()) is not { } store)
            return;

        ToggleUi(args.Performer, store.Owner, store.Comp, entity, entity.Comp);
    }
}
