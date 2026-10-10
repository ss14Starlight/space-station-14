using Content.Client._Starfall.Particles;
using Content.Shared._Funkystation.ReagentFires;
using Content.Shared._Funkystation.WallStains.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._Funkystation.WallStains.Systems;

public sealed partial class WallStainFireVisualsSystem : EntitySystem
{
    [Dependency] private ParticleSystem _particles = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private AppearanceSystem _appearance = default!;

    private struct StainEmitters
    {
        public ActiveEmitter? Fire;
        public ActiveEmitter? Embers;
        public ActiveEmitter? Slag;
        public ActiveEmitter? Sparks;
        public ActiveEmitter? Fumes;
    }

    private readonly Dictionary<EntityUid, StainEmitters> _emitters = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WallStainFireVisualsComponent, ComponentStartup>(OnCompStartup);
        SubscribeLocalEvent<WallStainFireVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<WallStainFireVisualsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnCompStartup(EntityUid uid, WallStainFireVisualsComponent component, ref ComponentStartup args)
    {
        UpdateVisuals(uid);
    }

    private void OnShutdown(EntityUid uid, WallStainFireVisualsComponent component, ref ComponentShutdown args)
    {
        if (_emitters.Remove(uid, out var pair))
        {
            _particles.RemoveParticle(pair.Fire);
            _particles.RemoveParticle(pair.Embers);
            _particles.RemoveParticle(pair.Slag);
            _particles.RemoveParticle(pair.Sparks);
            _particles.RemoveParticle(pair.Fumes);
        }
    }

    private void OnAppearanceChange(EntityUid uid, WallStainFireVisualsComponent component, ref AppearanceChangeEvent args)
    {
        UpdateVisuals(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        foreach (var (uid, pair) in _emitters)
        {
            if (Deleted(uid))
                continue;
            var coords = _transform.GetMapCoordinates(uid);
            if (pair.Fire is { Exhausted: false })
                pair.Fire.MapCoords = coords;
            if (pair.Embers is { Exhausted: false })
                pair.Embers.MapCoords = coords;
            if (pair.Slag is { Exhausted: false })
                pair.Slag.MapCoords = coords;
            if (pair.Sparks is { Exhausted: false })
                pair.Sparks.MapCoords = coords;
            if (pair.Fumes is { Exhausted: false })
                pair.Fumes.MapCoords = coords;
        }
    }

    private void UpdateEmitter(ref ActiveEmitter? emitter, string effectId, MapCoordinates coords, EntityUid uid, float intensity, Color color)
    {
        if (emitter == null || emitter.Exhausted)
            emitter = _particles.SpawnEffect(effectId, coords, uid);

        if (emitter != null)
        {
            emitter.Intensity = intensity;
            emitter.ColorOverride = color;
        }
    }

    private void UpdateVisuals(EntityUid uid)
    {
        if (!_emitters.TryGetValue(uid, out var pair))
            pair = new StainEmitters();

        var coords = _transform.GetMapCoordinates(uid);

        var fireState = 4;
        if (_appearance.TryGetData<int>(uid, ReagentPuddleFireVisuals.FireState, out var state))
            fireState = state;

        var color = Color.White;
        if (_appearance.TryGetData<Color>(uid, ReagentPuddleFireVisuals.FireColor, out var c))
            color = c;

        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            sprite.LayerSetState(0, fireState.ToString());
            sprite.Color = color;
        }

        var baseIntensity = fireState == 6 ? 2.0f : fireState == 5 ? 1.5f : 1.0f;
        var metalFireIntensity = fireState >= 5 ? baseIntensity : 0f;

        UpdateEmitter(ref pair.Fire, "WallFire", coords, uid, baseIntensity, color);
        UpdateEmitter(ref pair.Embers, "WallFireEmbers", coords, uid, baseIntensity, color);
        UpdateEmitter(ref pair.Slag, "WallFireSlag", coords, uid, metalFireIntensity, color);
        UpdateEmitter(ref pair.Sparks, "WallFireSparks", coords, uid, metalFireIntensity, color);
        UpdateEmitter(ref pair.Fumes, "WallFireFumes", coords, uid, metalFireIntensity, color.WithAlpha(0.35f));

        _emitters[uid] = pair;
    }
}
