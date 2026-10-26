using System.Linq;
using Content.Server.Objectives.Components;
using Content.Server.PDA.Ringer;
using Content.Shared._Starlight.Implants.Components;
using Content.Server.Store.Systems;
using Content.Server.StoreDiscount.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.PDA;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Traitor.Uplink;

public sealed partial class UplinkSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private StoreSystem _store = default!;
    [Dependency] private SharedSubdermalImplantSystem _subdermalImplant = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private RingerSystem _ringer = default!;

    public static readonly EntProtoId<StoreComponent> TraitorUplinkStore = "StorePresetRemoteUplink";
    public static readonly ProtoId<CurrencyPrototype> TelecrystalCurrencyPrototype = "Telecrystal";
    private static readonly EntProtoId FallbackUplinkImplant = "UplinkImplant";
    private static readonly ProtoId<ListingPrototype> FallbackUplinkCatalog = "UplinkUplinkImplanter";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RemoteStoreComponent, ImplantImplantedEvent>(OnRemoteStoreImplanted);
    }

    private void OnRemoteStoreImplanted(Entity<RemoteStoreComponent> entity, ref ImplantImplantedEvent args)
    {
        if (!HasComp<UplinkImplantComponent>(entity))
            return;

        if (_mind.GetMind(args.Implanted) is not { } mind)
            return;

        var storeEnumerator = EntityQueryEnumerator<RingerAccessUplinkComponent, StoreComponent>();
        while (storeEnumerator.MoveNext(out var uid, out _, out var store))
        {
            if (store.AccountOwner != mind)
                continue;

            entity.Comp.Store = uid;
            return;
        }

        entity.Comp.Store = Spawn(TraitorUplinkStore, MapCoordinates.Nullspace);
        SetUplink(args.Implanted, entity.Comp.Store.Value, 0, true);
        Log.Error($"{ToPrettyString(args.Implanted)} did not have an uplink when they were implanted.");
    }

    /// <summary>
    /// Adds an uplink to the target.
    /// The store lives in nullspace. A PDA or implant only holds a <see cref="RemoteStoreComponent"/> pointed at it.
    /// </summary>
    public AddUplinkResult AddUplink(
        EntityUid user,
        FixedPoint2 balance,
        out Note[]? code,
        EntityUid? uplinkEntity = null,
        bool giveDiscounts = false,
        bool bindToPda = false)
    {
        code = null;

        var storeEntity = Spawn(TraitorUplinkStore, MapCoordinates.Nullspace);
        if (TryAddEntityUplink(user, balance, out var generatedCode, uplinkEntity, storeEntity, giveDiscounts, bindToPda))
        {
            code = generatedCode;
            return AddUplinkResult.Pda;
        }

        if (TryImplantUplink(user, storeEntity, balance, giveDiscounts))
            return AddUplinkResult.Implant;

        Del(storeEntity);
        return AddUplinkResult.Failure;
    }

    public bool TryAddEntityUplink(
        EntityUid user,
        FixedPoint2 balance,
        out Note[]? code,
        EntityUid? uplinkEntity,
        EntityUid storeEntity,
        bool giveDiscounts = false,
        bool bindToPda = false)
    {
        code = null;
        uplinkEntity ??= FindUplinkTarget(user);

        if (uplinkEntity == null)
            return false;

        var ev = new GenerateUplinkCodeEvent();
        RaiseLocalEvent(storeEntity, ref ev);

        if (ev.Code == null)
        {
            QueueDel(storeEntity);
            return false;
        }

        code = ev.Code;

        if (bindToPda)
        {
            var accessComp = EnsureComp<RingerAccessUplinkComponent>(storeEntity);
            _ringer.SetBoundUplinkEntity((storeEntity, accessComp), uplinkEntity.Value);
        }

        EnsureComp<RemoteStoreComponent>(uplinkEntity.Value);
        SetUplink(user, storeEntity, balance, giveDiscounts);
        return true;
    }

    /// <summary>
    /// Configure TC for a nullspace uplink store.
    /// </summary>
    public void SetUplink(EntityUid user, EntityUid store, FixedPoint2 balance, bool giveDiscounts)
    {
        if (!_mind.TryGetMind(user, out var mind, out var mindUser))
            return;

        var storeComp = EnsureComp<StoreComponent>(store);
        storeComp.AccountOwner = mind;
        storeComp.Balance.Clear();
        _store.TryAddCurrency(new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { { TelecrystalCurrencyPrototype, balance } },
            store,
            storeComp);

        foreach (var objective in mindUser.Objectives)
        {
            if (!HasComp<DieConditionComponent>(objective))
                continue;

            DAGDUplinkExpansion(store);
            break;
        }

        var uplinkInitializedEvent = new StoreInitializedEvent(
            TargetUser: mind,
            Store: store,
            UseDiscounts: giveDiscounts,
            UseSecondHand: true,
            Listings: _store.GetAvailableListings(mind, store, storeComp).ToArray());
        RaiseLocalEvent(ref uplinkInitializedEvent);
    }

    /// <summary>
    /// Implant an uplink as a fallback measure if the traitor had no PDA.
    /// </summary>
    public bool TryImplantUplink(EntityUid user, EntityUid storeEntity, FixedPoint2 balance, bool giveDiscounts)
    {
        if (!_proto.Resolve<ListingPrototype>(FallbackUplinkCatalog, out var catalog))
            return false;

        if (!catalog.Cost.TryGetValue(TelecrystalCurrencyPrototype, out var cost))
            return false;

        if (balance < cost)
            balance = 0;
        else
            balance -= cost;

        SetUplink(user, storeEntity, balance, giveDiscounts);
        var implant = _subdermalImplant.AddImplant(user, FallbackUplinkImplant);

        if (!TryComp<RemoteStoreComponent>(implant, out var remote) || remote.Store == null)
        {
            Log.Error($"Implant does not have a remote store {implant}");
            return false;
        }

        return true;
    }

    public bool DAGDUplinkExpansion(EntityUid? uplinkEntity = null)
    {
        if (uplinkEntity == null)
            return false;

        var store = EnsureComp<StoreComponent>(uplinkEntity.Value);
        _store.TryAddCurrency(new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { { TelecrystalCurrencyPrototype, 15 } }, uplinkEntity.Value, store);
        return true;
    }

    /// <summary>
    /// Finds a PDA that can open a remote uplink for a user.
    /// </summary>
    public EntityUid? FindUplinkTarget(EntityUid user)
    {
        if (_inventorySystem.TryGetContainerSlotEnumerator(user, out var containerSlotEnumerator))
        {
            while (containerSlotEnumerator.MoveNext(out var containerSlot))
            {
                var pdaUid = containerSlot.ContainedEntity;

                if (pdaUid is { } pda && HasComp<PdaComponent>(pda) && HasComp<RemoteStoreComponent>(pda))
                    return pda;
            }
        }

        foreach (var item in _handsSystem.EnumerateHeld(user))
        {
            if (HasComp<PdaComponent>(item) && HasComp<RemoteStoreComponent>(item))
                return item;
        }

        return null;
    }
}

public enum AddUplinkResult
{
    Pda,
    Implant,
    Failure,
}
