using System.Numerics;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Actions.Events;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._Starlight.Actions.EntitySystems;

/// <summary>
/// Client-side visual effects for latching - currently just the K9's
/// head-shake on Bite Harder. SharedLatchSystem's handlers also run here.
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
    }

    private void OnBiteShake(LatchBiteShakeEvent ev)
    {
        var entity = GetEntity(ev.Latcher);
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        _animation.Play(entity, GetBiteShakeAnimation(sprite.Offset), BiteShakeAnimationKey);
    }

    /// <summary>
    /// A quick side-to-side wiggle back to the sprite's current offset, like
    /// a dog shaking its head mid-bite.
    /// </summary>
    private Animation GetBiteShakeAnimation(Vector2 startOffset)
    {
        var frameLength = ShakeLength / ShakeCount;
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
