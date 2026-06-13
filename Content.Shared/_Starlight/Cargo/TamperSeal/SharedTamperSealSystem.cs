using System.Linq;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TamperSealComponent, StorageOpenAttemptEvent>(OnStorageOpenAttempt);
        SubscribeLocalEvent<TamperSealComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<TamperSealComponent, TamperSealDestroyedDoAfterEvent>(OnDestroyDoAfter);
        SubscribeLocalEvent<TamperSealComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    #region Events

    private void OnAfterInteractUsing(EntityUid uid, TamperSealComponent component, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled || component.Opened)
            return;

        if (!TryDestroy(uid, args.Used, args.User, component))
            return;

        args.Handled = true;
    }

    private void OnDestroyDoAfter(EntityUid _, TamperSealComponent seal, ref TamperSealDestroyedDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || seal.Opened)
            return;

        if (args.Target == null)
            return;

        DoDestroy(args.Target.Value, args.User, seal);
        args.Handled = true;
    }

    private void OnStorageOpenAttempt(EntityUid uid, TamperSealComponent seal, ref StorageOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        if (seal.Opened)
            return;
        if (args.Silent)
            return;

        TryUnseal(uid, args.User, seal);

        // If unsealing succeeded, you should try to open it a second time.
        // If unsealing failed, it should not open.
        args.Cancelled = true;
    }

    private void OnGetAltVerbs(EntityUid uid, TamperSealComponent seal, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        if (seal.Opened)
            return;

        var user = args.User;
        var item = args.Using;
        var hasCorrectTool = item != null && _tool.HasQuality(item.Value, seal.DestroyTool);

        var verb = new AlternativeVerb()
        {
            Text = Loc.GetString("tamper-seal-verb-destroy"),
            IconEntity = hasCorrectTool ? GetNetEntity(item) : null,
            Disabled = !hasCorrectTool,
            Message = hasCorrectTool ? null : Loc.GetString("tamper-seal-verb-destroy-disabled"),
            Act = () =>
            {
                if (item != null)
                    TryDestroy(uid, item.Value, user, seal);
            },
            Priority = -10
        };
        args.Verbs.Add(verb);
    }

    #endregion

    #region API

    public bool TryUnseal(EntityUid uid, EntityUid actor, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return false;

        // Seal already opened one way or another; we call this success.
        if (seal.Opened)
            return true;

        // No access => Disallow
        if (!CanUnseal(uid, actor, seal))
        {
            _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-no-access"), uid, actor);
            return false;
        }

        DoUnseal(uid, actor, seal);
        return true;
    }

    public bool TryDestroy(EntityUid uid, EntityUid tool, EntityUid user, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal)) return false;
        if (!_tool.HasQuality(tool, seal.DestroyTool)) return false;

        // Show a popup.
        _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-destroy-begin"), uid, user, PopupType.LargeCaution);
        _audio.PlayPredicted(seal.DestroyBeginSound, uid, user);

        // I tried using ToolSystem.UseTool, but that causes mispredicts, linked to the AttemptFrequency.
        var args =
            new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(2.5f), new TamperSealDestroyedDoAfterEvent(), uid,
                target: uid, used: tool)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
                NeedHand = true,
                AttemptFrequency = AttemptFrequency.EveryTick,
            };

        return _doAfter.TryStartDoAfter(args);
    }

    #endregion

    #region Internal

    private bool CanUnseal(EntityUid uid, EntityUid actor, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return false;

        // If none are set, permit it.
        if (seal.Accesses.Count == 0)
            return true;

        var actorTags = _accessReader.FindAccessTags(actor);
        return seal.Accesses.Any(actorTags.Contains);
    }

    private void DoUnseal(EntityUid uid, EntityUid actor, TamperSealComponent seal)
    {
        if (seal.Opened)
            return;

        seal.Opened = true;
        _audio.PlayPredicted(seal.OpenSound, uid, actor);
        Appearance.SetData(uid, TamperSealVisuals.Opened, seal.Opened);
        Dirty(uid, seal);

        if (!TryComp<StationBankAccountComponent>(seal.RecipientStation, out var bank))
            return;

        // Reward cargo, and popup unsealing success + reward amount.
        // Note that there is no failure path here; we report it as if it succeeded, whether it did or not.
        _cargo.TryAdjustBankAccount((seal.RecipientStation, bank), seal.DelivererAccount, seal.RewardSpesos);
        _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-unlocked", ("reward", seal.RewardSpesos)), uid, actor);

        // TODO: admin log
    }

    private void DoDestroy(EntityUid uid, EntityUid actor, TamperSealComponent seal)
    {
        if (seal.Opened || seal.Destroyed)
            return;

        seal.Opened = true;
        seal.Destroyed = true;
        _audio.PlayPredicted(seal.DestroyEndSound, uid, actor);
        Appearance.SetData(uid, TamperSealVisuals.Opened, seal.Opened);
        Appearance.SetData(uid, TamperSealVisuals.Destroyed, seal.Destroyed);
        Dirty(uid, seal);

        if (!TryComp<StationBankAccountComponent>(seal.RecipientStation, out var bank))
            return;

        // Penalize the deliverer and compensate the recipient.
        // Note that there is no failure path here; we report it as if it succeeded, whether it did or not.
        _cargo.TryAdjustBankAccount((seal.RecipientStation, bank), seal.DelivererAccount, -seal.PenaltySpesos);
        _cargo.TryAdjustBankAccount((seal.RecipientStation, bank), seal.RecipientAccount, seal.PenaltyRefundSpesos);

        _popup.PopupPredicted(Loc.GetString(
            "tamper-seal-popup-destroy-end",
            ("penalty", seal.PenaltySpesos),
            ("refund", seal.PenaltyRefundSpesos)), uid, actor);

        // TODO: admin log

    }

    #endregion
}

[Serializable, NetSerializable]
public sealed partial class TamperSealDestroyedDoAfterEvent : SimpleDoAfterEvent;
