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
        }
    }

    private static bool HasDoAfterController(DoAfterArgs args) => args.HandController != null;

    private static string? GetDoAfterHand(DoAfterArgs args, string? fallback)
        => HasDoAfterController(args) ? args.ControlHand : fallback;

    private bool IsDoAfterControllerValid(DoAfter doAfter)
    {
        var args = doAfter.Args;
        return args.HandController is not { } controller
               || EntityManager.System<BodyControlHandContextSystem>()
                   .IsDoAfterHandValid(args.User, controller, doAfter.InitialHand, args.BreakOnHandChange);
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
