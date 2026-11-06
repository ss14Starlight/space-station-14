using Content.Shared._Starlight.Shadekin.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Shadekin;

public sealed partial class ShadegenSystem : EntitySystem
{
    [Dependency] private PointLightSystem _lightSys = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _updateQueue = new();

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateCooldown = TimeSpan.FromSeconds(0.2f);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_timing.RealTime < _nextUpdate)
            return;

        _nextUpdate = _timing.RealTime + _updateCooldown;

        var shadeQuery = EntityQueryEnumerator<ShadegenComponent>();

        foreach (var toUpdate in _updateQueue)
        {
            if (Deleted(toUpdate))
                continue;

            if (_container.TryGetContainingContainer(toUpdate, out var uidcontainer) && uidcontainer.OccludesLight)
                continue;

            if (TryComp<PointLightComponent>(toUpdate, out var lightcomp))
                _lightSys.SetContainerOccluded(toUpdate, false, lightcomp);
        }

        _updateQueue.Clear();

        while (shadeQuery.MoveNext(out var uid, out var shadegen))
        {
            if (Transform(uid).MapID == MapId.Nullspace)
                continue;

            var lightQuery = _lookup.GetEntitiesInRange<PointLightComponent>(Transform(uid).Coordinates, shadegen.Range);
            foreach (var light in lightQuery)
            {
                if (light.Comp.ContainerOccluded || HasComp<DarkLightComponent>(light))
                    continue;

                _lightSys.SetContainerOccluded(light.Owner, true, light.Comp);
                _updateQueue.Add(light.Owner);
            }
        }
    }
}
