using Content.Shared._Starlight.EndOfRoundGriefing.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Sticky;
using Content.Shared.Trigger;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.EndOfRoundGriefing;

/// <summary>
/// Intercepts various events and checks if they can be done.
/// </summary>
public abstract partial class SharedEorgPreventionSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = null!;

    protected bool IsEnabled = false;
    protected bool HasRoundEnded = false;
    protected bool IsPending = false; // Pending is true if something is preventing peace from spreading such as zeds or an active nukie trying to nuke CC.

    protected bool IsPreventionActive => IsEnabled && HasRoundEnded && !IsPending;

    [SubscribeLocalEvent]
    private void OnAttemptStick(EntityUid uid, EorgPreventStickingComponent component, ref AttemptEntityStickEvent args)
    {
        if (!IsPreventionActive) return;
        if (args.Cancelled) return;
        if (!HasComp<PreventEorgComponent>(args.User)) return;

        _popup.PopupPredicted(Loc.GetString("eorg-action"), args.User, args.User, PopupType.LargeCaution);
        args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnUseAttempt(EntityUid uid, EorgPreventUseItemComponent component, ref UseAttemptEvent args)
    {
        if (!IsPreventionActive) return;
        if (args.Cancelled) return;
        if (!HasComp<PreventEorgComponent>(uid)) return;

        _popup.PopupPredicted(Loc.GetString("eorg-action"), uid, uid, PopupType.LargeCaution);
        args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnAttemptTrigger(EntityUid uid, EorgPreventTriggerComponent component, ref AttemptTriggerEvent args)
    {
        if (!IsPreventionActive) return;
        if (!args.User.HasValue || args.Cancelled) return;
        if (!HasComp<PreventEorgComponent>(args.User)) return;

        _popup.PopupPredicted(Loc.GetString("eorg-action"), args.User.Value, args.User.Value, PopupType.LargeCaution);
        args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnBeforeInteractHand(EntityUid uid, EorgPreventHandInteractComponent component, ref BeforeInteractHandEvent args)
    {
        if (!IsPreventionActive) return;
        if (args.Handled) return;
        if (!HasComp<PreventEorgComponent>(uid)) return;

        _popup.PopupPredicted(Loc.GetString("eorg-action"), uid, uid, PopupType.LargeCaution);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnBeforeRangedInteract(EntityUid uid, EorgPreventRangedInteractComponent component, ref BeforeRangedInteractEvent args)
    {
        if (!IsPreventionActive) return;
        if (args.Handled) return;
        if (!HasComp<PreventEorgComponent>(args.User)) return;

        _popup.PopupPredicted(Loc.GetString("eorg-action"), args.User, args.User, PopupType.LargeCaution);
        args.Handled = true;
    }

    /// <summary>
    /// Prevents performing actions marked as <see cref="EorgPreventActionComponent"/> if the user has
    /// <see cref="PreventEorgComponent"/>.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnValidateAction(EntityUid uid, EorgPreventActionComponent component, ref ActionValidateEvent args)
    {
        if (!IsPreventionActive) return;
        if (args.Invalid) return;
        if (!HasComp<PreventEorgComponent>(args.User))  return;

        _popup.PopupPredicted(Loc.GetString("eorg-action"), args.User, args.User, PopupType.LargeCaution);
        args.Invalid = true;
    }

}

[Serializable, NetSerializable]
public sealed class RequestEorgPreventionStateEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class EorgPreventionStateEvent(bool isEnabled, bool hasRoundEnded, bool isPending) : EntityEventArgs
{
    public bool IsEnabled { get; } = isEnabled;
    public bool HasRoundEnded { get; } = hasRoundEnded;
    public bool IsPending { get; } = isPending;
}
