using Content.Shared._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Shadekin;

public sealed partial class ShadegenSystem : EntitySystem
{
    [Dependency] private PointLightSystem _lightSys = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ContainerSystem _container = default!;

    private readonly HashSet<EntityUid> _updateQueue = new();

    public override void Initialize()
        => base.Initialize();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

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
