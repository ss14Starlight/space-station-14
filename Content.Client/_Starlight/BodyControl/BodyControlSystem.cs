using Content.Shared._Starlight.BodyControl;
using Robust.Client.Physics;

namespace Content.Client._Starlight.BodyControl;

public sealed partial class BodyControlSystem : EntitySystem
{
    [Dependency] private Robust.Client.Physics.PhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeInteractions();
    }

    public override void Shutdown()
    {
        ShutdownInteractions();
        base.Shutdown();
    }

    [SubscribeLocalEvent]
    private void OnMoveInput(Entity<BodyControlSourceComponent> ent, ref BodyControlMoveInputEvent args)
    {
        if (ent.Comp.ExternallyDriven)
            return;
        // Returning from the mover handler still sends the original input through the normal engine protocol.
        args.Handled = !ent.Comp.Access.Allows("body.move")
                       || !TryComp<BodyControlTargetComponent>(ent.Comp.Body, out var target)
                       || target.ControllerCount != 1 || target.SoleController != ent.Owner;
    }

    [SubscribeLocalEvent]
    private void OnPrediction(Entity<BodyControlTargetComponent> ent, ref UpdateIsPredictedEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running && ent.Comp.ControllerCount > 1)
            args.BlockPrediction = true;
    }

    [SubscribeLocalEvent]
    private void OnSourcePrediction(Entity<BodyControlSourceComponent> ent, ref UpdateIsPredictedEvent args)
    {
        if (!ent.Comp.ExternallyDriven && ent.Comp.LifeStage <= ComponentLifeStage.Running && ent.Comp.Body != ent.Owner)
            args.BlockPrediction = true;
    }

    [SubscribeLocalEvent]
    private void OnTargetState(Entity<BodyControlTargetComponent> ent, ref AfterAutoHandleStateEvent args)
        => _physics.UpdateIsPredicted(ent);

    [SubscribeLocalEvent]
    private void OnTargetShutdown(Entity<BodyControlTargetComponent> ent, ref ComponentShutdown args)
    {
        _physics.UpdateIsPredicted(ent);
        RefreshControlView(ent);
    }

    [SubscribeLocalEvent]
    private void OnSourceState(Entity<BodyControlSourceComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _physics.UpdateIsPredicted(ent);
        _physics.UpdateIsPredicted(ent.Comp.Body);
        RefreshControlView(ent);
    }

    [SubscribeLocalEvent]
    private void OnSourceRemoved(Entity<BodyControlSourceComponent> ent, ref ComponentRemove args)
    {
        _physics.UpdateIsPredicted(ent);
        _physics.UpdateIsPredicted(ent.Comp.Body);
        RefreshControlView(ent);
    }

    partial void InitializeInteractions();
    partial void ShutdownInteractions();
    partial void RefreshControlView(EntityUid entity);
}
