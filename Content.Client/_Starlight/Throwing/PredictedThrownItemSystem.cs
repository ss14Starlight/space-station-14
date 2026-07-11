using Content.Shared._Starlight.Throwing;
using Robust.Client.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Throwing;

public sealed partial class PredictedThrownItemSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _phys = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PredictedThrownItemComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PredictedThrownItemComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PredictedThrownItemComponent, UpdateIsPredictedEvent>(OnUpdatePredicted);
    }

    private void OnStartup(Entity<PredictedThrownItemComponent> ent, ref ComponentStartup args) =>
        _phys.UpdateIsPredicted(ent.Owner);

    private void OnShutdown(Entity<PredictedThrownItemComponent> ent, ref ComponentShutdown args) =>
        Timer.Spawn(3000, () =>
        {
            _phys.UpdateIsPredicted(ent.Owner);
        });

    private void OnUpdatePredicted(Entity<PredictedThrownItemComponent> ent, ref UpdateIsPredictedEvent args) =>
        args.IsPredicted = true;
}
