using Content.Shared._Starlight.BodyControl;

// ReSharper disable CheckNamespace
namespace Content.Client.PhysicsSystem.Controllers;

public sealed partial class MoverController
{
    private bool CanPredictBodyControlMovement(EntityUid player)
    {
        // Each client knows only its own commands while the server arbitrates a shared body's movement.
        if (TryComp<BodyControlTargetComponent>(player, out var shared)
            && shared.LifeStage <= ComponentLifeStage.Running && shared.ControllerCount > 1)
            return false;
        if (TryComp<BodyControlSourceComponent>(player, out var source)
            && !source.ExternallyDriven && source.LifeStage <= ComponentLifeStage.Running && source.Body != player)
            return false;
        return true;
    }
}
