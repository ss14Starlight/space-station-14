using Content.Shared._Starlight.CosmicCult;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.NullSpace;

public sealed class NullSpaceBlockerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPvsOverrideSystem _pvs = default!;
    public EntProtoId _shadekinShadow = "ShadekinShadow";
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NullSpaceBlockerComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<NullSpaceBlockerComponent, ComponentShutdown>(OnCompShutdown);
        SubscribeLocalEvent<NullSpaceBlockerComponent, StartCollideEvent>(OnEntityEnter);
    }

    private void OnCompInit(Entity<NullSpaceBlockerComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.BypassPVS)
            _pvs.AddGlobalOverride(ent);
    }

    private void OnCompShutdown(Entity<NullSpaceBlockerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.BypassPVS)
            _pvs.RemoveGlobalOverride(ent);
    }

    private void OnEntityEnter(EntityUid uid, NullSpaceBlockerComponent component, ref StartCollideEvent args)
    {
        // Nullspace Related.
        if (component.UnphaseOnCollide)
            UnphaseOnCollide(args.OtherEntity);
    }

    private void UnphaseOnCollide(EntityUid uid)
    {
        if (!TryComp<NullSpaceComponent>(uid, out var nullspace))
            return;

        RemComp(uid, nullspace);

        if (_net.IsServer)
            SpawnAtPosition(_shadekinShadow, Transform(uid).Coordinates);
    }
}
