using Content.Shared._Starlight.BodyControl;
using Content.Shared.Hands.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.DoAfter;

public abstract partial class SharedDoAfterSystem
{
    protected virtual bool ShouldUpdateDoAfter(DoAfter doAfter) => true;

    // Retain the explicit controller even after its synchronous interaction returns.
    private void CaptureDoAfterController(DoAfterArgs args)
    {
        if (EntityManager.System<BodyControlHandContextSystem>().TryGetContext(args.User, out var control))
        {
            args.ControlHand = control.Hand;
            args.HandController = GetNetEntity(control.Controller);
            args.ControlPermission = control.Permission;
        }
    }

    private static bool HasDoAfterController(DoAfterArgs args) => args.HandController != null;

    private static string? GetDoAfterHand(DoAfterArgs args, string? fallback)
        => HasDoAfterController(args) ? args.ControlHand : fallback;

    private bool IsDoAfterControllerValid(DoAfter doAfter)
    {
        var args = doAfter.Args;
        if (args.HandController is not { } controller)
            return true;
        var context = EntityManager.System<BodyControlHandContextSystem>();
        if (!context.TryGetController(args.User, controller, out _, out var source)
            || args.ControlPermission is { } permission && !source.Access.Allows(permission))
            return false;
        return !args.NeedHand || args.ControlHand != null
               && context.AllowsHand(args.User, args.ControlHand, source.Access)
               && (!args.BreakOnHandChange || source.SelectedHand == args.ControlHand);
    }

    // Only synchronous callbacks inherit the initiating hand. Awaited continuations must pass it explicitly.
    private bool RunDoAfterWithController(DoAfter doAfter, Action callback)
    {
        var args = doAfter.Args;
        if (args.HandController is not { } controller)
        {
            callback();
            return true;
        }
        if (!doAfter.Cancelled && !IsDoAfterControllerValid(doAfter))
            return false;
        var context = EntityManager.System<BodyControlHandContextSystem>();
        if (context.TryGetController(args.User, controller, out var actor, out var source))
            context.Execute(args.User, actor, source.Access, args.ControlHand, callback, args.ControlPermission);
        else
            // Cancellation still reaches consumers for cleanup, without granting access to another hand.
            context.Execute(args.User, EntityUid.Invalid, new BodyControlAccess(), null, callback);
        return true;
    }

    private bool IsDoAfterItemHeld(DoAfter doAfter, HandsComponent hands)
    {
        var args = doAfter.Args;
        return HasDoAfterController(args)
            ? _hands.GetHeldItem((args.User, hands), doAfter.InitialHand) == doAfter.InitialItem
            : _hands.IsHolding((args.User, hands), doAfter.InitialItem);
    }

    private static bool HasDoAfterHandChanged(DoAfter doAfter, HandsComponent hands)
        => !HasDoAfterController(doAfter.Args) && hands.ActiveHandId != doAfter.InitialHand;
}
