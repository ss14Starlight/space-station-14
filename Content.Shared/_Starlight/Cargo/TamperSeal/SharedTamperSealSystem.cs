using System.Linq;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Access.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.TamperSeal;

public abstract partial class SharedTamperSealSystem : EntitySystem
{
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TamperSealComponent, StorageOpenAttemptEvent>(OnStorageOpenAttempt);
        SubscribeLocalEvent<TamperSealComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<TamperSealComponent, TamperSealViolatedDoAfterEvent>(OnViolateDoAfter);
        SubscribeLocalEvent<TamperSealComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    #region Events

    private void OnAfterInteractUsing(EntityUid uid, TamperSealComponent component, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryViolate(uid, args.Used, args.User, component))
            return;

        args.Handled = true;
    }

    private void OnViolateDoAfter(EntityUid _, TamperSealComponent seal, ref TamperSealViolatedDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target == null)
            return;

        DoViolate(args.Target.Value, args.User, seal);
        args.Handled = true;
    }

    private void OnStorageOpenAttempt(EntityUid uid, TamperSealComponent seal, ref StorageOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        if (seal.Opened)
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
        var hasCorrectTool = item != null && _tool.HasQuality(item.Value, seal.ViolateTool);

        var verb = new AlternativeVerb()
        {
            Text = Loc.GetString("tamper-seal-verb-cut"),
            IconEntity = hasCorrectTool ? GetNetEntity(item) : null,
            Disabled = !hasCorrectTool,
            Message = hasCorrectTool ? null : Loc.GetString("tamper-seal-verb-cut-disabled"),
            Act = () =>
            {
                if (item != null)
                    TryViolate(uid, item.Value, user, seal);
            }
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

    public bool TryViolate(EntityUid uid, EntityUid tool, EntityUid user, TamperSealComponent? seal = null)
    {
        if (!Resolve(uid, ref seal))
            return false;

        return _tool.UseTool(tool, user, uid, 2f, seal.ViolateTool, new TamperSealViolatedDoAfterEvent());
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
        seal.Opened = true;
        _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-unlocked"), uid, actor);
        Appearance.SetData(uid, TamperSealVisuals.Opened, true);
        // TODO: admin log
        // TODO: play sound
        // TODO: credit cargo
        Dirty(uid, seal);
    }

    private void DoViolate(EntityUid uid, EntityUid actor, TamperSealComponent seal)
    {
        seal.Opened = true;
        seal.Violated = true;
        _popup.PopupPredicted(Loc.GetString("tamper-seal-popup-violated"), uid, actor, PopupType.LargeCaution);
        Appearance.SetData(uid, TamperSealVisuals.Opened, true);
        Appearance.SetData(uid, TamperSealVisuals.Violated, true);
        // TODO: admin log
        // TODO: play sound
        // TODO: credit cargo
        Dirty(uid, seal);
    }

    #endregion

}

[Serializable, NetSerializable]
public sealed partial class TamperSealViolatedDoAfterEvent : SimpleDoAfterEvent;
