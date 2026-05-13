using Content.Client.Examine;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Client._FarHorizons.Power.Generation.FissionGenerator;

// This file used to have code that was under the following license:
// Ported and modified from goonstation by Jhrushbe.
// CC-BY-NC-SA-3.0
// https://github.com/goonstation/goonstation/blob/ff86b044/code/obj/nuclearreactor/turbine.dm

public sealed class GasTurbineSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    private readonly float _threshold = 1f;
    private float _accumulator = 0;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasTurbineComponent, ClientExaminedEvent>(TurbineExamined);

        SubscribeLocalEvent<GasTurbineComponent, AnimationCompletedEvent>(OnAnimationCompleted);

        SubscribeLocalEvent<GasTurbineComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<GasTurbineComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
    }

    private void TurbineExamined(EntityUid uid, GasTurbineComponent comp, ClientExaminedEvent args) => Spawn(comp.ArrowPrototype, new EntityCoordinates(uid, 0, 0));

    #region Animation
    private void OnAnimationCompleted(EntityUid uid, GasTurbineComponent comp, ref AnimationCompletedEvent args) => PlayAnimation(uid, comp);

    public override void FrameUpdate(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator >= _threshold)
        {
            AccUpdate();
            _accumulator = 0;
        }
    }

    private void AccUpdate()
    {
        var query = EntityQueryEnumerator<GasTurbineComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            // Makes sure the anim doesn't get stuck at low RPM
            PlayAnimation(uid, component);

            UpdateParticles(uid, component);
        }
    }

    private void PlayAnimation(EntityUid uid, GasTurbineComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !_sprite.TryGetLayer((uid,sprite), GasTurbineVisualLayers.TurbineSpeed, out var layer, false))
            return;

        var state = "speedanim";
        if (comp.RPM < 1)
        {
            _animationPlayer.Stop(uid, state);
            _sprite.LayerSetRsiState(layer, "turbine");
            comp.AnimRPM = -comp.BestRPM; // Primes it to start the instant it's spinning again
            return;
        }

        if (Math.Abs(comp.RPM - comp.AnimRPM) > comp.BestRPM * 0.1)
            _animationPlayer.Stop(uid, state); // Current anim is stale, time for a new one

        if (_animationPlayer.HasRunningAnimation(uid, state))
            return;

        comp.AnimRPM = comp.RPM;
        var layerKey = GasTurbineVisualLayers.TurbineSpeed;
        var time = 0.5f * comp.BestRPM / comp.RPM;
        var timestep = time / 12;
        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(time),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = layerKey,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_00", 0),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_01", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_02", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_03", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_04", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_05", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_06", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_07", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_08", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_09", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_10", timestep),
                        new AnimationTrackSpriteFlick.KeyFrame("turbinerun_11", timestep)
                    }
                }
            }
        };
        _sprite.LayerSetVisible(layer, true);
        _animationPlayer.Play(uid, animation, state);
    }
    #endregion

    // If there was a particle system, I would use it, for now I'm just stealing the jetpack's system like I'm not supposed to
    private void UpdateParticles(EntityUid uid, GasTurbineComponent comp)
    {
        if(!comp.IsSmoking && !comp.IsSparking)
            return;

        var uidXform = Transform(uid);

        var coordinates = uidXform.Coordinates;
        var gridUid = _transformSystem.GetGrid(coordinates);

        if (TryComp<MapGridComponent>(gridUid, out var grid))
        {
            coordinates = new EntityCoordinates(gridUid.Value, _mapSystem.WorldToLocal(gridUid.Value, grid, _transformSystem.ToMapCoordinates(coordinates).Position));
        }
        else if (uidXform.MapUid != null)
        {
            coordinates = new EntityCoordinates(uidXform.MapUid.Value, _transformSystem.GetWorldPosition(uidXform));
        }
        else
        {
            return;
        }

        if(comp.IsSparking)
            Spawn("GasTurbineSparkEffect", coordinates.Offset(new(_random.NextFloat(-1, 1),_random.NextFloat(-1, 1))));

        if(comp.IsSmoking)
            Spawn("GasTurbineSmokeEffect", coordinates.Offset(new(_random.NextFloat(-1, 1),_random.NextFloat(-1, 1))));
    }

    private void OnEjectAttempt(EntityUid uid, GasTurbineComponent comp, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (comp.RPM < 1)
            return;

        args.Cancelled = true;
    }

    private void OnInsertAttempt(EntityUid uid, GasTurbineComponent comp, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (comp.RPM < 1)
            return;

        args.Cancelled = true;
    }
}
