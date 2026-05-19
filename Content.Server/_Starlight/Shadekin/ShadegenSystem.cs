using Content.Server.Light.EntitySystems;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class ShadegenSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PoweredLightSystem _light = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HandheldLightSystem _handheldLight = default!;
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _shadegenLights = new();
    private readonly Dictionary<EntityUid, int> _lightAffectCounts = new();
    private readonly HashSet<EntityUid> _nextAffected = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadegenComponent, EntityTerminatingEvent>(OnShadegenTerminating);
        SubscribeLocalEvent<ShadegenAffectedComponent, EntityTerminatingEvent>(OnAffectedLightTerminating);
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

            RefreshShadegen(uid, component);
        }
    }

    private void RefreshShadegen(EntityUid uid, ShadegenComponent component)
    {
        if (!_shadegenLights.TryGetValue(uid, out var affectedLights))
        {
            affectedLights = new HashSet<EntityUid>();
            _shadegenLights[uid] = affectedLights;
        }

        _nextAffected.Clear();

        var lightQuery = _lookup.GetEntitiesInRange<PointLightComponent>(Transform(uid).Coordinates, component.Range);
        foreach (var light in lightQuery)
        {
            if (HasComp<DarkLightComponent>(light.Owner))
                continue;

            _nextAffected.Add(light.Owner);

            if (TryComp<HandheldLightComponent>(light.Owner, out var handheldcomp) && handheldcomp.Activated)
                _handheldLight.TurnOff((light.Owner, handheldcomp), makeNoise: false);

            if (component.DestroyLights && TryComp<PoweredLightComponent>(light.Owner, out var poweredcomp) && poweredcomp.On)
            {
                if (_light.TryDestroyBulb(light.Owner, poweredcomp))
                    RaiseLocalEvent(Transform(uid).ParentUid, new OnLightBreakEvent(light.Owner));
            }
        }

        foreach (var previous in affectedLights)
        {
            if (_nextAffected.Contains(previous))
                continue;

            RemoveLightAffect(previous);
        }

        foreach (var current in _nextAffected)
        {
            if (affectedLights.Contains(current))
                continue;

            if (_lightAffectCounts.TryGetValue(current, out var count))
            {
                _lightAffectCounts[current] = count + 1;
            }
            else
            {
                _lightAffectCounts[current] = 1;
                EnsureComp<ShadegenAffectedComponent>(current);
            }
        }

        affectedLights.Clear();
        foreach (var current in _nextAffected)
        {
            affectedLights.Add(current);
        }
    }

    private void OnShadegenTerminating(EntityUid uid, ShadegenComponent component, EntityTerminatingEvent args)
    {
        if (!_shadegenLights.Remove(uid, out var affectedLights))
            return;

        foreach (var light in affectedLights)
        {
            RemoveLightAffect(light);
        }
    }

    private void OnAffectedLightTerminating(EntityUid uid, ShadegenAffectedComponent component, EntityTerminatingEvent args)
    {
        _lightAffectCounts.Remove(uid);

        foreach (var affectedLights in _shadegenLights.Values)
        {
            affectedLights.Remove(uid);
        }
    }

    private void RemoveLightAffect(EntityUid uid)
    {
        if (!_lightAffectCounts.TryGetValue(uid, out var count))
            return;

        if (count > 1)
        {
            _lightAffectCounts[uid] = count - 1;
            return;
        }

        _lightAffectCounts.Remove(uid);

        if (!Deleted(uid))
            RemComp<ShadegenAffectedComponent>(uid);
    }
}
