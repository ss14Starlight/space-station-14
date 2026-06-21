using Content.Client._Starfall.Particles;
using Content.Shared._Funkystation.ReagentFires;
using Content.Shared._Starfall.Particles;
using Robust.Client.GameObjects;

namespace Content.Client._Funkystation.ReagentFires.Systems
{
    public sealed class ReagentPuddleFireVisualsSystem : EntitySystem
    {
        [Dependency] private readonly AppearanceSystem _appearance = null!;
        [Dependency] private readonly ParticleSystem _particles = null!;
        [Dependency] private readonly SharedTransformSystem _transform = null!;

        private readonly Dictionary<EntityUid, (ActiveEmitter? Fire, ActiveEmitter? Smoke)> _emitters = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ReagentPuddleFireEffectComponent, ComponentStartup>(OnCompStartup);
            SubscribeLocalEvent<ReagentPuddleFireEffectComponent, AppearanceChangeEvent>(OnAppearanceChange);
            SubscribeLocalEvent<ReagentPuddleFireEffectComponent, ComponentShutdown>(OnShutdown);
        }

        private void OnCompStartup(EntityUid uid, ReagentPuddleFireEffectComponent component, ref ComponentStartup args)
        {
            UpdateVisuals(uid, null);
        }

        private void OnShutdown(EntityUid uid, ReagentPuddleFireEffectComponent component, ref ComponentShutdown args)
        {
            if (_emitters.Remove(uid, out var pair))
            {
                _particles.RemoveParticle(pair.Fire);
                _particles.RemoveParticle(pair.Smoke);
            }
        }

        private void OnAppearanceChange(EntityUid uid, ReagentPuddleFireEffectComponent component, ref AppearanceChangeEvent args)
        {
            UpdateVisuals(uid, args.Sprite);
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

                if (pair.Smoke is { Exhausted: false })
                    pair.Smoke.MapCoords = coords;
            }
        }

        private void UpdateVisuals(EntityUid uid, SpriteComponent? sprite)
        {
            if (sprite == null && !TryComp(uid, out sprite))
                return;

            if (!_emitters.TryGetValue(uid, out var pair))
            {
                pair = (null, null);
            }

            var coords = _transform.GetMapCoordinates(uid);
            var updated = false;

            if (pair.Fire == null || pair.Fire.Exhausted)
            {
                pair.Fire = _particles.SpawnEffect("ReagentFireContinuous", coords, uid);
                updated = true;
            }

            if (pair.Smoke == null || pair.Smoke.Exhausted)
            {
                pair.Smoke = _particles.SpawnEffect("ReagentFireSmoke", coords, uid);
                updated = true;
            }

            if (updated)
            {
                _emitters[uid] = pair;
            }

            if (!_appearance.TryGetData<int>(uid, ReagentPuddleFireVisuals.FireState, out var fireState))
            {
                fireState = 4;
            }

            var stateString = fireState.ToString();
            sprite.LayerSetState(0, stateString);

            var intensity = 1f;
            var smokeSize = 0.8f;
            if (fireState == 5)
            {
                intensity = 1.5f;
                smokeSize = 1.2f;
            }
            else if (fireState == 6)
            {
                intensity = 2.0f;
                smokeSize = 1.6f;
            }

            if (pair.Fire != null)
                pair.Fire.Intensity = intensity;
            if (pair.Smoke != null)
            {
                pair.Smoke.Intensity = intensity;
                var smokeOverrides = new ParticleRuntimeOverrides { ParticleSize = smokeSize };
                ParticleSystem.UpdateRuntime(pair.Smoke, smokeOverrides);
            }

            // Apply synchronized flame color dynamically to the decoupled sprite and standard particle emitters
            if (_appearance.TryGetData<Color>(uid, ReagentPuddleFireVisuals.FireColor, out var color))
            {
                sprite.Color = color;
                if (pair.Fire != null)
                    pair.Fire.ColorOverride = color;
                // Soften the smoke tint opacity slightly so it acts as a subtle background element
                if (pair.Smoke != null)
                    pair.Smoke.ColorOverride = color.WithAlpha(0.25f);
            }
        }
    }
}
