using Content.Shared.Access.Components;
using Content.Server.Hands.Systems;
using Content.Shared.Starlight;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.PlayerVendor;
using Content.Shared.IdentityManagement;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Server.Player;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Content.Server.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Item;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio;
using Content.Shared.Destructible;
using Content.Shared.Damage;
using Content.Shared.UserInterface;

namespace Content.Server.PlayerVendor;

public sealed class PlayerVendorSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly ISharedPlayersRoleManager _playerRoles = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedItemSystem _itemSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private static readonly ProtoId<TagPrototype> _highRisk = "HighRiskItem";
    private static readonly ProtoId<RadioChannelPrototype> _securityChannel = "Security";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerVendorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PlayerVendorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerb);
        SubscribeLocalEvent<PlayerVendorComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUiOpenAttempt);
        SubscribeLocalEvent<PlayerVendorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PlayerVendorComponent, BreakageEventArgs>(OnBreakage);
        SubscribeLocalEvent<PlayerVendorComponent, DamageChangedEvent>(OnDamageChanged);

        Subs.BuiEvents<PlayerVendorComponent>(PlayerVendorUiKey.Key, subs =>
        {
            subs.Event<PlayerVendorPurchaseMessage>(OnPurchaseMessage);
            subs.Event<PlayerVendorSetPriceMessage>(OnSetPriceMessage);
            subs.Event<PlayerVendorWithdrawMessage>(OnWithdrawMessage);
            subs.Event<PlayerVendorToggleLockMessage>(OnToggleLockMessage);
            subs.Event<PlayerVendorClaimOwnershipMessage>(OnClaimMessage);
            subs.Event<PlayerVendorRefundDepositMessage>(OnRefundDeposit);
        });
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var enumerator = EntityQueryEnumerator<PlayerVendorComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.OwnerEntity == null)
                continue;
            var ownerNet = comp.OwnerEntity.Value;
            var ownerUid = GetEntity(ownerNet);
            if (!EntityManager.EntityExists(ownerUid) || Deleted(ownerUid))
            {
                comp.OwnerEntity = null;
                comp.OwnerName = null;
                comp.Locked = true;
                Dirty(uid, comp);
                PushState((uid, comp));
            }
        }
    }

    private bool IsPowered(EntityUid uid)
    {
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var _))
            return true;

        return _power.IsPowered(uid);
    }

    private void OnBreakage(EntityUid uid, PlayerVendorComponent comp, BreakageEventArgs args)
    {
        if (comp.Broken)
            return;
        comp.Broken = true;
        comp.Locked = true;
        Dirty(uid, comp);
        _uiSystem.CloseUi(uid, PlayerVendorUiKey.Key);
        UpdateAppearance((uid, comp));
    }

    private void OnDamageChanged(EntityUid uid, PlayerVendorComponent comp, DamageChangedEvent args)
    {
        if (!comp.Broken)
            return;
        if (args.DamageIncreased)
            return;
        if (TryComp<DamageableComponent>(uid, out var dmg) && dmg.TotalDamage == 0)
        {
            comp.Broken = false;
            Dirty(uid, comp);
            UpdateAppearance((uid, comp));
        }
    }

    private void OnActivatableUiOpenAttempt(Entity<PlayerVendorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-broken"), ent, args.User);
            args.Cancel();
            return;
        }
    }

    private void UpdateAppearance(Entity<PlayerVendorComponent> ent)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        var state = PlayerVendorVisualState.Normal;
        if (ent.Comp.Broken)
            state = PlayerVendorVisualState.Broken;
        else if (!IsPowered(ent)) 
            state = PlayerVendorVisualState.Off;

        _appearance.SetData(ent, PlayerVendorVisuals.VisualState, state, appearance);
    }

    private void OnExamined(Entity<PlayerVendorComponent> ent, ref ExaminedEvent args)
    {
        var owner = ent.Comp.OwnerEntity == null ? Loc.GetString("player-vendor-examine-no-owner") : Loc.GetString("player-vendor-examine-owner", ("owner", ent.Comp.OwnerName ?? "?"));
        args.PushMarkup(owner);
        if (ent.Comp.OwnerEntity == null)
            args.PushMarkup(Loc.GetString("player-vendor-examine-claim-hint"));
        args.PushMarkup(ent.Comp.Locked ? Loc.GetString("player-vendor-examine-locked") : Loc.GetString("player-vendor-examine-unlocked"));
        if (IsOwner(ent, args.Examiner))
            args.PushMarkup(Loc.GetString("player-vendor-examine-balance", ("amount", ent.Comp.Balance)));
    }

    private void OnInteractUsing(Entity<PlayerVendorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (args.Used != EntityUid.Invalid && TryComp<StackComponent>(args.Used, out var stack) && stack.StackTypeId == "NTCredit")
        {
            var userId = GetUserId(args.User);
            if (userId == null)
                return;

            if (ent.Comp.CurrentDepositorUserId != null && ent.Comp.CurrentDepositorUserId != userId)
            {
                _popup.PopupEntity(Loc.GetString("player-vendor-popup-other-depositor-active"), ent, args.User);
                _audio.PlayPvs(ent.Comp.DenySound, ent);
                args.Handled = true;
                return;
            }

            var add = stack.Count;
            if (add > 0)
            {
                ent.Comp.CurrentDepositorUserId ??= userId;
                ent.Comp.CurrentDepositAmount += add;
                Del(args.Used);
                Dirty(ent);
                PushState(ent);
            }
            args.Handled = true;
            return;
        }

        if (args.Used != EntityUid.Invalid && TryComp<IdCardComponent>(args.Used, out var idCard))
        {
            TryClaimOrToggle(ent, args.User, idCard);
            args.Handled = true;
            return;
        }

        if (!_hands.CanDrop(args.User, args.Used))
            return;

        args.Handled = DoInsert(ent, args.User, args.Used);
    }

    private bool DoInsert(Entity<PlayerVendorComponent> ent, EntityUid user, EntityUid used)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return false;

        if (ent.Comp.Locked && !IsOwner(ent, user))
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return true;
        }

        if (!TryComp<ItemComponent>(used, out var itemComp))
        {
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-invalid-not-item"), ent, user);
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return true;
        }

        if (HasComp<IdCardComponent>(used))
        {
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-id-card"), ent, user);
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return true;
        }

        var sizeProto = _itemSystem.GetSizePrototype(itemComp.Size);
        var normalProto = _itemSystem.GetSizePrototype("Normal");
        if (sizeProto > normalProto)
        {
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-item-too-large"), ent, user);
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return true;
        }

        if (_tagSystem.HasTag(used, _highRisk))
        {
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-high-risk"), ent, user);
            var userName = Identity.Name(user, EntityManager);
            var itemName = Identity.Name(used, EntityManager);
            var vendorName = Identity.Name(ent.Owner, EntityManager);
            _radio.SendRadioMessage(ent.Owner, Loc.GetString("player-vendor-radio-attempt-high-risk", ("user", userName), ("item", itemName), ("vendor", vendorName)), _securityChannel, ent.Owner);
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return true;
        }

        if (HasComp<GunComponent>(used) ||
            (TryComp<MeleeWeaponComponent>(used, out var melee) && melee.Damage.AnyPositive()))
        {
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-weapon-disallowed"), ent, user);
            var userName = Identity.Name(user, EntityManager);
            var itemName = Identity.Name(used, EntityManager);
            var vendorName = Identity.Name(ent.Owner, EntityManager);
            _radio.SendRadioMessage(ent.Owner, Loc.GetString("player-vendor-radio-attempt-weapon", ("user", userName), ("item", itemName), ("vendor", vendorName)), _securityChannel, ent.Owner);
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return true;
        }

        var entryName = Identity.Name(used, EntityManager);
        if (!ent.Comp.Entries.Contains(entryName))
            ent.Comp.Entries.Add(entryName);
        ent.Comp.ContainedEntries.TryAdd(entryName, new());
        var set = ent.Comp.ContainedEntries[entryName];

        if (set.Count >= ent.Comp.MaxItemsPerEntry)
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return true;
        }

        _container.Insert(used, container);
        var net = GetNetEntity(used);
        set.Add(net);
        if (!ent.Comp.Prices.ContainsKey(entryName))
            ent.Comp.Prices[entryName] = ent.Comp.DefaultPrice;
        Dirty(ent);
        PushState(ent);
        _audio.PlayPvs(ent.Comp.InsertSound, ent);
        return true;
    }

    private void OnGetAltVerb(Entity<PlayerVendorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (args.Using is { } item)
        {
            var user = args.User;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("verb-categories-insert"),
                Act = () => DoInsert(ent, user, item)
            });
        }

        if (ent.Comp.OwnerEntity == null)
        {
            var user = args.User;
            if (TryComp<HandsComponent>(user, out var hands) && _hands.TryGetActiveItem(user, out var held) && held != null)
            {
                if (TryComp<IdCardComponent>(held, out _))
                {
                    args.Verbs.Add(new AlternativeVerb
                    {
                        Text = Loc.GetString("player-vendor-verb-claim"),
                        Act = () =>
                        {
                            if (TryComp<IdCardComponent>(held, out var id))
                                TryClaimOrToggle(ent, user, id);
                        }
                    });
                }
            }
        }
    }

    private void OnPurchaseMessage(Entity<PlayerVendorComponent> ent, ref PlayerVendorPurchaseMessage msg)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        var actor = msg.Actor;

        if (!ent.Comp.ContainedEntries.TryGetValue(msg.Entry, out var set) || set.Count == 0)
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return;
        }

        var price = ent.Comp.Prices.GetValueOrDefault(msg.Entry, ent.Comp.DefaultPrice);
        var userId = GetUserId(actor);
        if (userId == null)
            return;

        if (ent.Comp.CurrentDepositorUserId != userId)
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-no-active-deposit"), ent, actor);
            return;
        }
        if (ent.Comp.CurrentDepositAmount < price)
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return;
        }

        ent.Comp.CurrentDepositAmount -= price;
        var newBalance = ent.Comp.Balance + price;
        ent.Comp.Balance = Math.Min(newBalance, ent.Comp.MaxBalance);
        if (ent.Comp.CurrentDepositAmount <= 0)
        {
            ent.Comp.CurrentDepositAmount = 0;
            ent.Comp.CurrentDepositorUserId = null;
        }

        foreach (var net in set)
        {
            var eid = GetEntity(net);
            if (_container.TryRemoveFromContainer(eid))
            {
                set.Remove(net);
                break;
            }
        }

        _audio.PlayPvs(ent.Comp.VendSound, ent);
        Dirty(ent);
        PushState(ent);
    }

    private void OnSetPriceMessage(Entity<PlayerVendorComponent> ent, ref PlayerVendorSetPriceMessage msg)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (msg.Price < 0) msg.Price = 0;
        if (msg.Price > ent.Comp.MaxPricePerItem) msg.Price = ent.Comp.MaxPricePerItem;
        if (!ent.Comp.Entries.Contains(msg.Entry))
            return;
        ent.Comp.Prices[msg.Entry] = msg.Price;
        Dirty(ent);
        PushState(ent);
    }

    private void OnWithdrawMessage(Entity<PlayerVendorComponent> ent, ref PlayerVendorWithdrawMessage msg)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        var actor = msg.Actor;

        if (ent.Comp.Balance <= 0)
            return;

        var amount = ent.Comp.Balance;
        var spawnCoords = Transform(ent).Coordinates;
        var spawned = Spawn("NTCredit", spawnCoords);
        if (TryComp<StackComponent>(spawned, out var stack))
        {
            _stacks.SetCount(spawned, amount, stack);
        }
        ent.Comp.Balance = 0;
        Dirty(ent);
        PushState(ent);
    }

    private void OnRefundDeposit(Entity<PlayerVendorComponent> ent, ref PlayerVendorRefundDepositMessage msg)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        var actor = msg.Actor;
        var uid = GetUserId(actor);
        if (uid == null || ent.Comp.CurrentDepositorUserId != uid)
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return;
        }
        if (ent.Comp.CurrentDepositAmount <= 0)
            return;
        var amount = ent.Comp.CurrentDepositAmount;
        var spawnCoords = Transform(ent).Coordinates;
        var cash = Spawn("NTCredit", spawnCoords);
        if (TryComp<StackComponent>(cash, out var stack))
            _stacks.SetCount(cash, amount, stack);
        ent.Comp.CurrentDepositAmount = 0;
        ent.Comp.CurrentDepositorUserId = null;
        PushState(ent);
    }

    private void OnToggleLockMessage(Entity<PlayerVendorComponent> ent, ref PlayerVendorToggleLockMessage msg)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        var actor = msg.Actor;
        if (!IsOwner(ent, actor))
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return;
        }
        ent.Comp.Locked = !ent.Comp.Locked;
        Dirty(ent);
        PushState(ent);
    }

    private void OnClaimMessage(Entity<PlayerVendorComponent> ent, ref PlayerVendorClaimOwnershipMessage msg)
    {
        if (!_timing.IsFirstTimePredicted)
            return;
        var actor = msg.Actor;
        if (ent.Comp.OwnerEntity != null)
            return;

        if (!TryComp<HandsComponent>(actor, out var hands) ||
            !_hands.TryGetActiveItem(actor, out var held) ||
            held == null ||
            !TryComp<IdCardComponent>(held, out _))
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-need-id-card"), ent, actor);
            return;
        }

        ent.Comp.OwnerEntity = GetNetEntity(actor);
        ent.Comp.Locked = true;

        if (TryComp(actor, out MetaDataComponent? meta))
            ent.Comp.OwnerName = meta.EntityName;
        Dirty(ent);
        PushState(ent);
        _popup.PopupEntity(Loc.GetString("player-vendor-popup-claimed"), ent, actor);
    }

    private bool TryClaimOrToggle(Entity<PlayerVendorComponent> ent, EntityUid user, IdCardComponent _)
    {
        var userId = GetUserId(user);
        if (userId == null)
            return false;
        if (ent.Comp.OwnerEntity == null)
        {
            ent.Comp.OwnerEntity = GetNetEntity(user);
            ent.Comp.Locked = true;
            if (TryComp(user, out MetaDataComponent? meta))
                ent.Comp.OwnerName = meta.EntityName;
            Dirty(ent);
            PushState(ent);
            _popup.PopupEntity(Loc.GetString("player-vendor-popup-claimed"), ent, user);
            return true;
        }
        if (ent.Comp.OwnerEntity != null && ent.Comp.OwnerEntity == GetNetEntity(user))
        {
            ent.Comp.Locked = !ent.Comp.Locked;
            Dirty(ent);
            PushState(ent);
            return true;
        }
        _audio.PlayPvs(ent.Comp.DenySound, ent);
        _popup.PopupEntity(Loc.GetString("player-vendor-popup-not-owner"), ent, user);
        return true;
    }

    private bool IsOwner(Entity<PlayerVendorComponent> ent, EntityUid user)
    {
        if (ent.Comp.OwnerEntity == null)
            return false;
        return GetNetEntity(user) == ent.Comp.OwnerEntity;
    }

    private void PushState(Entity<PlayerVendorComponent> ent)
    {
        var amounts = new Dictionary<string, int>();
        foreach (var entry in ent.Comp.Entries)
        {
            if (ent.Comp.ContainedEntries.TryGetValue(entry, out var set))
                amounts[entry] = set.Count;
            else
                amounts[entry] = 0;
        }

        var reps = new Dictionary<string, NetEntity?>();
        foreach (var entry in ent.Comp.Entries)
        {
            if (ent.Comp.ContainedEntries.TryGetValue(entry, out var set) && set.Count > 0)
            {
                NetEntity? first = null;
                foreach (var uid in set)
                {
                    first = uid;
                    break;
                }
                reps[entry] = first;
            }
            else
            {
                reps[entry] = null;
            }
        }

        var state = new PlayerVendorUiState(
            new List<string>(ent.Comp.Entries),
            amounts,
            new Dictionary<string, int>(ent.Comp.Prices),
            reps,
            ent.Comp.DefaultPrice,
            ent.Comp.Balance,
            ent.Comp.Locked,
            ent.Comp.OwnerEntity,
            ent.Comp.OwnerName,
            ent.Comp.CurrentDepositAmount,
            ent.Comp.CurrentDepositorUserId,
            false,
            false,
            true
            );

        if (_uiSystem.TryGetOpenUi(ent.Owner, PlayerVendorUiKey.Key, out _))
        {
            _uiSystem.SetUiState(ent.Owner, PlayerVendorUiKey.Key, state);
        }
    }

    private string? GetUserId(EntityUid mob)
    {
        var session = GetPlayerSession(mob);
        return session?.UserId.UserId.ToString();
    }

    private ICommonSession? GetPlayerSession(EntityUid mob)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity == mob)
                return session;
        }
        return null;
    }

    private bool TryGetPlayerBalance(EntityUid mob, out PlayerData data)
    {
        data = null!;
        var pdata = _playerRoles.GetPlayerData(mob);
        if (pdata == null)
            return false;
        data = pdata;
        return true;
    }
}
