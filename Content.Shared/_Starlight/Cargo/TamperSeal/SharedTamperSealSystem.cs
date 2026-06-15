using System.Linq;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Localizations;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.TamperSeal;

public abstract partial class SharedTamperSealSystem : EntitySystem
{
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCargoSystem _cargo = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TamperSealComponent, StorageOpenAttemptEvent>(OnStorageOpenAttempt,
            before: [typeof(LockSystem)]);
        SubscribeLocalEvent<TamperSealComponent, LockToggleAttemptEvent>(OnLockToggleAttempt);

        SubscribeLocalEvent<TamperSealComponent, ActivateInWorldEvent>(OnActivateInWorld,
            before: [typeof(LockSystem)]);
        SubscribeLocalEvent<TamperSealComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<TamperSealComponent, TamperSealUnsealedDoAfterEvent>(OnUnsealDoAfter);
        SubscribeLocalEvent<TamperSealComponent, TamperSealDestroyedDoAfterEvent>(OnDestroyDoAfter);
        SubscribeLocalEvent<TamperSealComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);

        SubscribeLocalEvent<TamperSealComponent, ExaminedEvent>(OnExamined);
    }

    #region Events

    /// <summary>
    /// If there is an unbroken tamper seal, disallow unlocking the tamper-sealed container.
    /// </summary>
    private void OnLockToggleAttempt(EntityUid uid, TamperSealComponent seal, ref LockToggleAttemptEvent args)
    {
        if (seal.Opened || args.Cancelled)
            return;

        args.Cancelled = true;
    }

    /// <summary>
    /// Try to unseal on interacting (pressing E).
    /// </summary>
    private void OnActivateInWorld(EntityUid uid, TamperSealComponent seal, ref ActivateInWorldEvent args)
    {
        if (seal.Opened || args.Handled)
            return;

        TryUnseal(uid, args.User, seal);

        // If unsealing succeeded, you should try to open it a second time.
        // If unsealing failed, it should not open.
        args.Handled = true;
    }

    /// <summary>
    /// Try to destroy on interacting with an item, e.g. holding a knife.
    /// </summary>
    private void OnAfterInteractUsing(EntityUid uid, TamperSealComponent component, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled || component.Opened)
            return;

        args.Handled = TryDestroy(uid, args.Used, args.User, component);
    }

    /// <summary>
    /// Try to unseal when attempting to open a sealed storage container.
    /// </summary>
    private void OnStorageOpenAttempt(EntityUid uid, TamperSealComponent seal, ref StorageOpenAttemptEvent args)
    {
        if (args.Cancelled || args.Silent || seal.Opened)
            return;

        TryUnseal(uid, args.User, seal);

        // If unsealing succeeded, you should try to open it a second time.
        // If unsealing failed, it should not open.
        args.Cancelled = true;
    }

    /// <summary>
    /// Add a right-click option to destroy the tamper seal.
    /// </summary>
    private void OnGetAltVerbs(EntityUid uid, TamperSealComponent seal, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || seal.Opened)
            return;

        var user = args.User;
        var item = args.Using;
        var hasCorrectTool = item != null && _tool.HasQuality(item.Value, seal.DestroyToolQuality);

        var verb = new AlternativeVerb()
        {
            Text = Loc.GetString("tamper-seal-verb-destroy"),
            IconEntity = hasCorrectTool ? GetNetEntity(item) : null,
            Message = Loc.GetString(hasCorrectTool
                ? "tamper-seal-verb-destroy-tool-description"
                : "tamper-seal-verb-destroy-hands-description"),
            Act = () =>
            {
                TryDestroy(uid, item, user, seal);
            },
            Priority = 50
        };
        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Adds short examine text saying a tamper seal is present, and which access(es) can unseal it.
    /// </summary>
    private void OnExamined(EntityUid uid, TamperSealComponent seal, ExaminedEvent args)
    {
        if (seal.Opened)
            return;

        // When there are no access levels specified, it's basically AA, so we have a different locale string for that.
        if (seal.Accesses.Count == 0)
        {
            args.PushMarkup(Loc.GetString("tamper-seal-examine-public-access-description"));
            return;
        }

        // Collect one string per access level, localized and colorized.
        var names = seal.Accesses
            .Select(id => _proto.TryIndex(id, out var proto)
                ? proto.GetAccessLevelName()
                : Loc.GetString("access-reader-unknown-id"))
            .Select(name => Loc.GetString("access-reader-access-label", ("access", name)))
            .ToList();

        // Concatenate the accesses as "or" in a locale-sensitive manner.
        var formatted = ContentLocalizationManager.FormatListToOr(names);

        // High-priority text so it shows at the top, since the tamper seal is the first thing you need to deal with
        // when interacting with an entity that has one.
        args.PushMarkup(Loc.GetString("tamper-seal-examine-private-access-description",
            ("access", formatted)), 100);
    }

    #endregion

    #region Do-afters

    private void OnUnsealDoAfter(EntityUid uid, TamperSealComponent seal, TamperSealUnsealedDoAfterEvent args)
    {
        if (args.Handled || seal.Opened || args.Target == null)
            return;

        if (args.Cancelled)
        {
            _adminLogger.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(args.User):player} stopped unsealing the tamper seal ({string.Join(",", seal.Accesses):accesses}) on {ToPrettyString(uid)}.");
            return;
        }

        DoUnseal(args.Target.Value, args.User, seal);
        args.Handled = true;
    }

    private void OnDestroyDoAfter(EntityUid uid, TamperSealComponent seal, ref TamperSealDestroyedDoAfterEvent args)
    {
        if (args.Handled || seal.Opened || args.Target == null)
            return;

        if (args.Cancelled)
        {
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(args.User):player} stopped destroying the {seal.RecipientAccount.Id} tamper seal on {ToPrettyString(uid)}.");
            return;
        }

        DoDestroy(args.Target.Value, args.User, seal);
        args.Handled = true;
    }

    #endregion

    #region API

    private void TryUnseal(EntityUid uid, EntityUid user, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return;

        if (seal.Opened)
            return;

        // If they have no access we just tell them.
        if (!CanUnseal(uid, user, seal))
        {
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-unseal-no-access"), uid, user);
            _adminLogger.Add(LogType.InteractActivate, LogImpact.Low,
                $"{ToPrettyString(user):player} had no access to unseal the {seal.RecipientAccount.Id} tamper seal on {ToPrettyString(uid)}. ({string.Join(",", seal.Accesses):accesses})");
            return;
        }

        _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-unseal-begin"), uid, user);
        _audio.PlayPredicted(seal.UnsealBeginSound, uid, user);

        // Start the do-after to unseal. It's short but not instant so that you can cancel if you do it accidentally.
        var args =
            new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(seal.UnsealTime),
                new TamperSealUnsealedDoAfterEvent(), uid,
                target: uid)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
                NeedHand = true,
                AttemptFrequency = AttemptFrequency.EveryTick,
            };

        _doAfter.TryStartDoAfter(args);
        _adminLogger.Add(LogType.InteractActivate, LogImpact.Low,
            $"{ToPrettyString(user):player} began unsealing the {seal.RecipientAccount.Id} tamper seal on {ToPrettyString(uid)}.");
    }

    private bool TryDestroy(EntityUid uid, EntityUid? tool, EntityUid user, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return false;

        var hasTool = tool.HasValue && _tool.HasQuality(tool.Value, seal.DestroyToolQuality);
        var toolKind = seal.DestroyToolQuality.Id.ToLowerInvariant(); // "slicing" or "prying".

        // Show a popup and play sound.
        _popup.PopupPredicted(
            Loc.GetString($"tamper-seal-popup-destroy-{(hasTool ? toolKind : "hands")}-begin"),
            uid, user, PopupType.Large);
        _audio.PlayPredicted(seal.DestroyBeginSound, uid, user);

        // I tried using ToolSystem.UseTool, but that causes mispredicts due to setting a different AttemptFrequency.
        // Doing it manually like this with AttemptFrequency.EveryTick works perfectly.
        var args =
            new DoAfterArgs(EntityManager, user,
                TimeSpan.FromSeconds(hasTool ? seal.DestroyWithToolTime : seal.DestroyWithHandsTime),
                new TamperSealDestroyedDoAfterEvent(), uid,
                target: uid, used: tool)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
                NeedHand = true,
                AttemptFrequency = AttemptFrequency.EveryTick,
            };

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} began destroying the {seal.RecipientAccount.Id} tamper seal on {ToPrettyString(uid)}.");
        return _doAfter.TryStartDoAfter(args);
    }

    #endregion

    #region Internal

    private bool CanUnseal(EntityUid uid, EntityUid user, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return false;

        // If none are set, permit it.
        if (seal.Accesses.Count == 0)
            return true;

        var userTags = _accessReader.FindAccessTags(user);
        return seal.Accesses.Any(userTags.Contains);
    }

    private void DoUnseal(EntityUid uid, EntityUid user, TamperSealComponent seal)
    {
        if (seal.Opened)
            return;

        seal.Opened = true;

        _audio.PlayPredicted(seal.UnsealEndSound, uid, user);
        Appearance.SetData(uid, TamperSealVisuals.Opened, seal.Opened);
        Dirty(uid, seal);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} unsealed the {seal.RecipientAccount.Id} tamper seal on {ToPrettyString(uid)}.");

        // Reward deliverer, only if deliverer != recipient (otherwise easy infinite money).
        if (TryComp<StationBankAccountComponent>(seal.RecipientStation, out var bank) &&
            seal.DelivererAccount != seal.RecipientAccount &&
            seal.RewardSpesos > 0)
        {
            _audio.PlayPredicted(seal.RewardSound, uid, user);
            _cargo.TryAdjustBankAccount((seal.RecipientStation, bank), seal.DelivererAccount, seal.RewardSpesos);
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-unseal-end-reward", ("reward", seal.RewardSpesos)),
                uid, user);
        }
        else
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-unseal-end"), uid, user);

        // Lastly, raise an event on the station.
        RaiseLocalEvent(seal.RecipientStation, new TamperSealUnsealedEvent(uid, seal, user));
    }

    private void DoDestroy(EntityUid uid, EntityUid user, TamperSealComponent seal)
    {
        if (seal.Opened || seal.Destroyed)
            return;

        seal.Opened = true;
        seal.Destroyed = true;
        _audio.PlayPredicted(seal.DestroyEndSound, uid, user);
        Appearance.SetData(uid, TamperSealVisuals.Opened, seal.Opened);
        Appearance.SetData(uid, TamperSealVisuals.Destroyed, seal.Destroyed);
        Dirty(uid, seal);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} destroyed the {seal.RecipientAccount.Id} tamper seal on {ToPrettyString(uid)}.");

        if (seal.DelivererAccount != seal.RecipientAccount &&
            (seal.PenaltySpesos > 0 || seal.PenaltyRefundSpesos > 0) &&
            TryComp<StationBankAccountComponent>(seal.RecipientStation, out var bank) &&
            _cargo.TryAdjustBankAccount((seal.RecipientStation, bank), seal.DelivererAccount, -seal.PenaltySpesos) &&
            _cargo.TryAdjustBankAccount((seal.RecipientStation, bank), seal.RecipientAccount, seal.PenaltyRefundSpesos))
        {
            // If all money transfers, report to the user that the deliverer was punished.
            _audio.PlayPredicted(seal.PenaltySound, uid, user);
            _popup.PopupPredicted(Loc.GetString(
                "tamper-seal-popup-destroy-end-penalty",
                ("penalty", seal.PenaltySpesos),
                ("refund", seal.PenaltyRefundSpesos)), uid, user, PopupType.LargeCaution);
        }
        else
            // Otherwise just report the seal was broken.
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-destroy-end"), uid, user, PopupType.LargeCaution);

        // Lastly, raise an event on the station.
        RaiseLocalEvent(seal.RecipientStation, new TamperSealDestroyedEvent(uid, seal, user));
    }

    #endregion
}

[Serializable, NetSerializable]
public sealed partial class TamperSealUnsealedDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TamperSealDestroyedDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public abstract class BaseTamperSealEvent(EntityUid uid, TamperSealComponent tamperSeal) : EntityEventArgs
{
    [DataField]
    public EntityUid SealEntity { get; } = uid;
    [DataField]
    public TamperSealComponent Seal { get; } = tamperSeal;
}

[Serializable, NetSerializable]
public sealed partial class TamperSealUnsealedEvent(
    EntityUid uid,
    TamperSealComponent tamperSeal,
    EntityUid actor)
    : BaseTamperSealEvent(uid, tamperSeal)
{
    [DataField]
    public EntityUid Actor { get; } = actor;
}

[Serializable, NetSerializable]
public sealed partial class TamperSealDestroyedEvent(
    EntityUid uid,
    TamperSealComponent tamperSeal,
    EntityUid actor)
    : BaseTamperSealEvent(uid, tamperSeal)
{
    [DataField]
    public EntityUid Actor { get; } = actor;
}
