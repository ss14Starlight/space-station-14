using System.Numerics;
using Content.Client._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Actions.Events;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._Starlight.Actions.EntitySystems;

/// <summary>
/// Client-side visual effects for latching (K9 head-shake on Bite Harder).
/// </summary>
public sealed partial class LatchSystem : SharedLatchSystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private const string BiteShakeAnimationKey = "latch-bite-shake";
    private const float ShakeLength = 0.25f;
    private const int ShakeCount = 4;
    private const float ShakeMagnitude = 0.08f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<LatchBiteShakeEvent>(OnBiteShake);
        SubscribeLocalEvent<LatchBiteShakeVisualsComponent, AnimationCompletedEvent>(OnShakeAnimationCompleted);
    }

    private void OnBiteShake(LatchBiteShakeEvent ev)
    {
        var entity = GetEntity(ev.Latcher);
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        var visuals = EnsureComp<LatchBiteShakeVisualsComponent>(entity);
        if (!_animation.HasRunningAnimation(entity, BiteShakeAnimationKey))
            visuals.BaseOffset = sprite.Offset;

        _animation.Play(entity, GetBiteShakeAnimation(visuals.BaseOffset), BiteShakeAnimationKey);
    }

    private void OnShakeAnimationCompleted(Entity<LatchBiteShakeVisualsComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key == BiteShakeAnimationKey && args.Finished)
            RemComp<LatchBiteShakeVisualsComponent>(ent);
    }

    /// <summary>
    /// Quick side-to-side wiggle back to the sprite's current offset.
    /// </summary>
    private Animation GetBiteShakeAnimation(Vector2 startOffset)
    {
        // ShakeCount shakes plus one return-to-rest segment.
        var frameLength = ShakeLength / (ShakeCount + 1);
        var keyFrames = new List<AnimationTrackProperty.KeyFrame> { new(startOffset, 0f) };

        for (var i = 0; i < ShakeCount; i++)
        {
            var x = (i % 2 == 0 ? 1f : -1f) * ShakeMagnitude;
            keyFrames.Add(new AnimationTrackProperty.KeyFrame(startOffset + new Vector2(x, 0f), frameLength));
        }

        keyFrames.Add(new AnimationTrackProperty.KeyFrame(startOffset, frameLength));

        return new Animation
        {
            Length = TimeSpan.FromSeconds(ShakeLength),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames = keyFrames,
                },
            },
        };
    }
}
