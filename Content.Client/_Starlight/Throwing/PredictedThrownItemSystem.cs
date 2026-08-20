using Content.Shared._Starlight.Throwing;
using Content.Shared.Throwing;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Throwing;

public sealed partial class PredictedThrownItemSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _phys = default!;
    [Dependency] private IPlayerManager _plr = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PredictedThrownItemComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PredictedThrownItemComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PredictedThrownItemComponent, UpdateIsPredictedEvent>(OnUpdatePredicted);
    }

    private void OnStartup(Entity<PredictedThrownItemComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<ThrownItemComponent>(ent, out var thrown)) return;
        if (_plr.LocalEntity is null) return;
        if (thrown.Thrower != _plr.LocalEntity.Value) return; // This always ends up being 0 on other clients and 0 on the last frame of prediction... SURELY NOT AN ISSUE...
        _phys.UpdateIsPredicted(ent.Owner);
    }

    private void OnShutdown(Entity<PredictedThrownItemComponent> ent, ref ComponentShutdown args) =>
        Timer.Spawn(3000, () =>
        {
            _phys.UpdateIsPredicted(ent.Owner);
        });

    private void OnUpdatePredicted(Entity<PredictedThrownItemComponent> ent, ref UpdateIsPredictedEvent args)
    {
        if (!TryComp<ThrownItemComponent>(ent, out var thrown)) return;
        if (_plr.LocalEntity is null) return;
        if (thrown.Thrower != _plr.LocalEntity.Value) return;
        args.IsPredicted = true;
    }
}
