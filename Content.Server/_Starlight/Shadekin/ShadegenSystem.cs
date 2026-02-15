using Content.Server.Light.EntitySystems;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class ShadegenSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PoweredLightSystem _light = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandheldLightSystem _handheldLight = default!;
    private readonly HashSet<EntityUid> _updateQueue = new();

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShadegenComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextUpdate)
                continue;

            component.NextUpdate = _timing.CurTime + component.UpdateCooldown;

            foreach (var toUpdate in _updateQueue)
            {
                if (Deleted(toUpdate))
                    continue;

                RemComp<ShadegenAffectedComponent>(toUpdate);
            }

            _updateQueue.Clear();

            var lightQuery = _lookup.GetEntitiesInRange<PointLightComponent>(Transform(uid).Coordinates, component.Range);
            foreach (var light in lightQuery)
            {
                if (HasComp<DarkLightComponent>(light.Owner))
                    continue;

                EnsureComp<ShadegenAffectedComponent>(light.Owner);
                _updateQueue.Add(light.Owner);

                if (TryComp<HandheldLightComponent>(light.Owner, out var handheldcomp) && handheldcomp.Activated)
                    _handheldLight.TurnOff(new Entity<HandheldLightComponent>(light.Owner, handheldcomp));

                if (component.DestroyLights && TryComp<PoweredLightComponent>(light.Owner, out var poweredcomp) && poweredcomp.On)
                    if (_light.TryDestroyBulb(light.Owner, poweredcomp))
                        RaiseLocalEvent(Transform(uid).ParentUid, new OnLightBreakEvent(light.Owner));
            }
        }
    }
}
