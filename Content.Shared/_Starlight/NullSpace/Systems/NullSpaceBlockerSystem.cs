using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.NullSpace;

public sealed class NullSpaceBlockerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    public EntProtoId _shadekinShadow = "ShadekinShadow";
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NullSpaceBlockerComponent, StartCollideEvent>(OnEntityEnter);
    }

    private void OnEntityEnter(EntityUid uid, NullSpaceBlockerComponent component, ref StartCollideEvent args)
    {
        if (!component.UnphaseOnCollide)
            return;

        var otherUid = args.OtherEntity;

        if (!TryComp<NullSpaceComponent>(otherUid, out var nullspace))
            return;

        RemComp(otherUid, nullspace);

        if (_net.IsServer)
            SpawnAtPosition(_shadekinShadow, Transform(otherUid).Coordinates);
    }
}
