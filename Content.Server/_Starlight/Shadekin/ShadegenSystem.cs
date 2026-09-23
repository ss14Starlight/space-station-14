using Content.Shared._Starlight.Railroading.Components.Tasks;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class ShadegenSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPoweredLightSystem _light = default!;
    [Dependency] private SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private SharedLightTreeSystem _lightTree = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<DarkLightComponent> _darkLightQuery = default!;

    private readonly List<Entity<SharedPointLightComponent, TransformComponent>> _lightsInRange = new();
    private readonly HashSet<EntityUid> _affected = new();
    private readonly List<EntityUid> _noLongerAffected = new();

    private bool _refreshQueued;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadegenComponent, ComponentShutdown>(OnShadegenShutdown);
    }

    private void OnShadegenShutdown(Entity<ShadegenComponent> ent, ref ComponentShutdown args)
        => _refreshQueued = true;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShadegenComponent>();
        while (query.MoveNext(out _, out var shadegen))
        {
            if (_timing.CurTime < shadegen.NextUpdate)
                continue;

            shadegen.NextUpdate = _timing.CurTime + shadegen.UpdateCooldown;
            _refreshQueued = true;
        }

        if (!_refreshQueued)
            return;

        _refreshQueued = false;
        RefreshAffectedLights();
    }

    /// <summary>
    /// Rebuilds the set of lights covered by any shadegen, only touching
    /// <see cref="ShadegenAffectedComponent"/> on the lights that entered or left a field.
    /// </summary>
    private void RefreshAffectedLights()
    {
        _affected.Clear();

        var query = EntityQueryEnumerator<ShadegenComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shadegen, out var xform))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            var coords = _transform.GetMapCoordinates(xform);
            if (coords.MapId == MapId.Nullspace)
                continue;

            var bounds = new Box2(coords.Position, coords.Position).Enlarged(shadegen.Range);

            _lightsInRange.Clear();
            _lightTree.QueryAabb(_lightsInRange, coords.MapId, bounds);

            foreach (var light in _lightsInRange)
            {
                if (_darkLightQuery.HasComp(light.Owner) || TerminatingOrDeleted(light.Owner))
                    continue;

                // Tree bounds are enlarged by each light's own radius, so drop the ones really out of range.
                if ((_transform.GetWorldPosition(light.Comp2) - coords.Position).LengthSquared() > shadegen.Range * shadegen.Range)
                    continue;

                _affected.Add(light.Owner);

                if (TryComp<HandheldLightComponent>(light.Owner, out var handheldcomp) && handheldcomp.Activated)
                    _handheldLight.TurnOff((light.Owner, handheldcomp), makeNoise: false);

                if (shadegen.DestroyLights && TryComp<PoweredLightComponent>(light.Owner, out var poweredcomp) && poweredcomp.On)
                    if (_light.TryDestroyBulb(light.Owner, poweredcomp))
                        RaiseLocalEvent(xform.ParentUid, new OnLightBreakEvent(light.Owner));
            }
        }

        _noLongerAffected.Clear();
        var affectedQuery = EntityQueryEnumerator<ShadegenAffectedComponent>();
        while (affectedQuery.MoveNext(out var uid, out _))
        {
            if (!_affected.Contains(uid))
                _noLongerAffected.Add(uid);
        }

        foreach (var uid in _noLongerAffected)
        {
            if (!TerminatingOrDeleted(uid))
                RemComp<ShadegenAffectedComponent>(uid);
        }

        foreach (var uid in _affected)
        {
            if (!TerminatingOrDeleted(uid))
                EnsureComp<ShadegenAffectedComponent>(uid);
        }
    }
}
