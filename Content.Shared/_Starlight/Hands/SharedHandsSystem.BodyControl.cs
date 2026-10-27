using Content.Shared._Starlight.BodyControl;

// ReSharper disable CheckNamespace
namespace Content.Shared.Hands.EntitySystems;

public abstract partial class SharedHandsSystem
{
    private string? GetControlHand(EntityUid body, string? fallback)
        => EntityManager.System<BodyControlHandContextSystem>().GetSelectedHand(body, fallback);

    private bool AllowsControlHand(EntityUid body, string hand)
        => EntityManager.System<BodyControlHandContextSystem>().AllowsHand(body, hand);

    private bool AllowsControlHandInteraction(EntityUid body, string hand)
    {
        var context = EntityManager.System<BodyControlHandContextSystem>();
        if (!context.AllowsHand(body, hand))
            return false;
        // Moving or combining two hand slots needs an explicit two-hand command.
        return !TryComp<BodyControlSourceComponent>(body, out var source) || source.ExternallyDriven
               || source.Body != body || source.Access.FullControl || context.IsExecuting(body);
    }

    private bool RelayControlHandSelection(EntityUid body, string? hand)
    {
        if (!TryComp<BodyControlSourceComponent>(body, out var source) || source.ExternallyDriven || source.Body != body)
            return false;
        var request = new BodyControlHandRequestEvent(BodyControlCommand.SelectHand, hand);
        RaiseLocalEvent(body, ref request);
        return true;
    }

    private bool RelayControlHandActivation(EntityUid body, string? hand)
        => RelayControlHandRequest(body, BodyControlCommand.ActivateHand, hand);

    private bool RelayControlHandUse(EntityUid body, bool altInteract, string? hand)
        => RelayControlHandRequest(body, altInteract ? BodyControlCommand.AltUseHand : BodyControlCommand.UseHand, hand);

    private bool RelayControlHandRequest(EntityUid body, BodyControlCommand command, string? hand)
    {
        if (!TryComp<BodyControlSourceComponent>(body, out var source) || source.ExternallyDriven || source.Body != body
            || EntityManager.System<BodyControlHandContextSystem>().IsExecuting(body))
            return false;
        var request = new BodyControlHandRequestEvent(command, hand);
        RaiseLocalEvent(body, ref request);
        return true;
    }
}
