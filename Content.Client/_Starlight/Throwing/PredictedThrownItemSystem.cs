using Content.Client._Starlight.Computers.RemoteControl;
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
    [Dependency] private RemoteControlInterface _remoteControl = default!;

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
        if (!IsLocallyControlledThrower(thrown.Thrower)) return;
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
        if (!IsLocallyControlledThrower(thrown.Thrower)) return;
        args.IsPredicted = true;
    }

    private bool IsLocallyControlledThrower(EntityUid? thrower)
        => thrower is { } entity
            && (entity == _plr.LocalEntity || entity == _remoteControl.ControlledEntity);
}
