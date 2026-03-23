using Content.Shared.Examine;

namespace Content.Shared._FarHorizons.VisualPickupable;

public abstract class SharedVisualPickupableSystem : EntitySystem
{
    public override void Initialize() => 
        SubscribeLocalEvent<PickupableVisualsComponent, ExamineAttemptEvent>(OnExamineAttempt);

    private void OnExamineAttempt(Entity<PickupableVisualsComponent> ent, ref ExamineAttemptEvent args) => 
        args.Cancel();
}