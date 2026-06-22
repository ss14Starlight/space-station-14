using System.Linq;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Localizations;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.TamperSeal;

/// <summary>
/// Manages tamper-sealed containers. Can be used independently of the cargo system or the tamper seal reward system.
/// </summary>
public abstract partial class SharedTamperSealSystem : EntitySystem
{
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Direct interactions with a tamper-sealed entity.
        SubscribeLocalEvent<TamperSealComponent, StorageOpenAttemptEvent>(OnStorageOpenAttempt,
            before: [typeof(LockSystem)]);
        SubscribeLocalEvent<TamperSealComponent, LockToggleAttemptEvent>(OnLockToggleAttempt);
        SubscribeLocalEvent<TamperSealComponent, ActivateInWorldEvent>(OnActivateInWorld,
            before: [typeof(LockSystem)]);
        SubscribeLocalEvent<TamperSealComponent, InteractUsingEvent>(OnInteractUsing);

        // Do-after completions
        SubscribeLocalEvent<TamperSealComponent, TamperSealUnsealedDoAfterEvent>(OnUnsealDoAfter);
        SubscribeLocalEvent<TamperSealComponent, TamperSealDestroyedDoAfterEvent>(OnDestroyDoAfter);

        // Right-click, Examine
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
    private void OnInteractUsing(EntityUid uid, TamperSealComponent component, ref InteractUsingEvent args)
    {
        if (args.Handled || component.Opened)
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
        var tool = args.Using;
        var matchingToolTypes = tool.HasValue
            ? seal.DestroyToolQualities
                .Where(quality => _tool.HasQuality(tool.Value, quality))
                .ToList()
            : new();
        var hasCorrectTool = matchingToolTypes.Count > 0;

        var verb = new AlternativeVerb()
        {
            Text = Loc.GetString("tamper-seal-verb-destroy"),
            IconEntity = hasCorrectTool ? GetNetEntity(tool) : null,
            Message = Loc.GetString(hasCorrectTool
                ? "tamper-seal-verb-destroy-tool-description"
                : "tamper-seal-verb-destroy-hands-description"),
            Act = () =>
            {
                TryDestroy(uid, tool, user, seal);
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
        if (seal.Opened) // Closed seals & Destroyed seals have Examine text.
            return;

        // High-priority text so it shows at the top, since the tamper seal is the first thing you need to deal with
        // when interacting with an entity that has one.
        args.PushMarkup(Loc.GetString("tamper-seal-examine-sealed-restricted",
            ("recipient", GetLocRecipientName(seal)),
            ("recipientColor", seal.RecipientExamineColor)), 100);
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
                $"{ToPrettyString(args.User):player} stopped destroying the {GetLocRecipientName(seal)} tamper seal on {ToPrettyString(uid)}.");
            return;
        }

        DoDestroy(args.Target.Value, seal, args.User);
        args.Handled = true;
    }

    #endregion

    #region Internal

    private void TryUnseal(EntityUid uid, EntityUid user, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return;

        if (seal.Opened)
            return;

        // If they have no access, we just tell them.
        if (!CanUnseal(uid, user, seal))
        {
            _popup.PopupClient(Loc.GetString("tamper-seal-popup-unseal-no-access"),
                uid, user, PopupType.Medium);
            _adminLogger.Add(LogType.InteractActivate, LogImpact.Low,
                $"{ToPrettyString(user):player} had no access to unseal the {GetLocRecipientName(seal)} tamper seal on {ToPrettyString(uid)}. ({string.Join(",", seal.Accesses):accesses})");
            return;
        }

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

        if (!_doAfter.TryStartDoAfter(args))
            return;

        // Show a popup and play sound.
        _popup.PopupClient(Loc.GetString("tamper-seal-popup-unseal-begin"),
            uid, user, PopupType.Medium);
        _audio.PlayPredicted(seal.UnsealBeginSound, uid, user);

        _adminLogger.Add(LogType.InteractActivate, LogImpact.Low,
            $"{ToPrettyString(user):player} began unsealing the {GetLocRecipientName(seal)} tamper seal on {ToPrettyString(uid)}.");
    }

    private bool TryDestroy(EntityUid uid, EntityUid? held, EntityUid user, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return false;

        var hasTool = held.HasValue && HasComp<ToolComponent>(held.Value);
        var matchingToolTypes = held.HasValue
            ? seal.DestroyToolQualities
                .Where(quality => _tool.HasQuality(held.Value, quality))
                .ToList()
            : new();
        var hasCorrectTool = matchingToolTypes.Count > 0;
        var toolKind = matchingToolTypes
            .Select(type => type.ToString().ToLowerInvariant())
            .FirstOrDefault("hands"); // "slicing", "cutting", "prying" or "hands".

        var qualities = ContentLocalizationManager.FormatListToOr(seal.DestroyToolQualities
            .Select(type => _proto.Index(type).Name)
            .Select(nameLoc => Loc.GetString(nameLoc))
            .ToList());

        // If the held item is not a tool, just abort.
        if (held.HasValue && !hasTool)
            return false;

        // I'd love to "return true" to block any interaction, but it also breaks other things like forensic scanners.]
        // Despite returning false for that case we still give a popup.
        if (hasTool && !hasCorrectTool)
        {
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-destroy-tool-required",
                    ("qualities", qualities)),
                uid, user, PopupType.Medium);
            return true;
        }

        // If the user is not using the correct tool (and is thus using their hands), but has no hands,
        // then we tell them they either need hands or the correct tool (the latter being for cyborgs).
        if (toolKind == "hands" && (!TryComp<HandsComponent>(user, out var hands) || hands.Count < 1))
        {
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-destroy-hands-or-tool-required",
                    ("qualities", qualities)),
                uid, user, PopupType.Medium);
            return true;
        }

        // I tried using ToolSystem.UseTool, but that causes mispredicts due to setting a different AttemptFrequency.
        // Doing it manually like this with AttemptFrequency.EveryTick works perfectly.
        var args =
            new DoAfterArgs(EntityManager, user,
                TimeSpan.FromSeconds(hasCorrectTool ? seal.DestroyWithToolTime : seal.DestroyWithHandsTime),
                new TamperSealDestroyedDoAfterEvent(), uid,
                target: uid, used: held)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
                NeedHand = true,
                AttemptFrequency = AttemptFrequency.EveryTick,
            };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        // Show a popup and play sound.
        _popup.PopupPredicted(
            Loc.GetString($"tamper-seal-popup-destroy-{toolKind}-begin"),
            uid, user, PopupType.LargeCaution);
        _audio.PlayPredicted(seal.DestroyBeginSound, uid, user);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} began destroying the {GetLocRecipientName(seal)} tamper seal on {ToPrettyString(uid)}.");
        return true;
    }

    private bool CanUnseal(EntityUid uid, EntityUid user, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return false;

        var userTags = _accessReader.FindAccessTags(user);
        if (seal.Accesses.Count == 0)
            return true;

        foreach (var pattern in seal.Accesses)
        {
            // If any of the AllOf accesses is absent, the pattern fails.
            if (pattern.AllOf is {} allOf && !allOf.All(userTags.Contains))
                continue;

            // If any of the NoneOf accesses is present, the pattern fails.
            if (pattern.NoneOf is {} noneOf && noneOf.Any(userTags.Contains))
                continue;

            // Success if we haven't failed.
            return true;
        }

        return false;
    }

    private void DoUnseal(EntityUid uid, EntityUid user, TamperSealComponent seal)
    {
        if (seal.Opened)
            return;

        seal.Opened = true;
        Dirty(uid, seal);

        Appearance.SetData(uid, TamperSealVisuals.Opened, seal.Opened);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} unsealed the {GetLocRecipientName(seal)} tamper seal on {ToPrettyString(uid)}.");

        // Notify any interested listeners.
        var ev = new TamperSealOpenedEvent(seal, user);
        RaiseLocalEvent(uid, ref ev);

        // Show popup unless disabled.
        if (ev.PlaySound)
            _audio.PlayPredicted(seal.UnsealEndSound, uid, user); // The sound is shared.
        if (ev.ShowPopup)
            _popup.PopupClient(Loc.GetString("tamper-seal-popup-unseal-end"), uid, user, PopupType.Medium); // Popup is personal.
    }

    protected void DoDestroy(EntityUid uid, TamperSealComponent seal, EntityUid? user = null,
        bool entityDestroyed = false, bool serverOnly = false)
    {
        if (seal.Opened || seal.Destroyed)
            return;

        seal.Opened = true;
        seal.Destroyed = true;
        Dirty(uid, seal);

        if (!entityDestroyed)
        {
            Appearance.SetData(uid, TamperSealVisuals.Opened, seal.Opened);
            Appearance.SetData(uid, TamperSealVisuals.Destroyed, seal.Destroyed);
        }

        // Log the destruction.
        if (user != null)
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):player} destroyed the {GetLocRecipientName(seal)} tamper seal on {ToPrettyString(uid)}");
        else
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"Unknown source destroyed the {GetLocRecipientName(seal)} tamper seal on {ToPrettyString(uid)}");

        // Notify any interested parties.
        var ev = new TamperSealDestroyedEvent(seal, user, entityDestroyed, serverOnly);
        RaiseLocalEvent(uid, ref ev);

        if (ev.PlaySound)
        {
            if (serverOnly)
                _audio.PlayPvs(seal.DestroyEndSound, uid);
            else
                _audio.PlayPredicted(seal.DestroyEndSound, uid, user);
        }

        if (!ev.ShowPopup)
            return;

        if (serverOnly)
            _popup.PopupEntity(Loc.GetString("tamper-seal-popup-destroy-end"), uid, PopupType.LargeCaution);
        else
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-destroy-end"), uid, user, PopupType.LargeCaution);
    }

    /// <summary>
    /// Localizes the recipient name, using the literal value if it fails to resolve.
    /// This enables manual overriding of the key with a literal value without erroring/warning.
    /// </summary>
    private string GetLocRecipientName(TamperSealComponent seal)
    {
        if (!Loc.TryGetString(seal.RecipientName, out var recipientName))
            recipientName = seal.RecipientName;
        return recipientName;
    }

    #endregion
}

[Serializable, NetSerializable]
public sealed partial class TamperSealUnsealedDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TamperSealDestroyedDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public record struct TamperSealOpenedEvent(
    TamperSealComponent Seal,
    EntityUid User,
    bool PlaySound = true,
    bool ShowPopup = true);

[ByRefEvent]
public record struct TamperSealDestroyedEvent(
    TamperSealComponent Seal,
    EntityUid? User,
    bool EntityDestroyed,
    bool ServerOnly,
    bool PlaySound = true,
    bool ShowPopup = true);

