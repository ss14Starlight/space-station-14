using System.Numerics;
using Content.Shared._Daiquiri.Emoting;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Daiquiri.Emoting;

public sealed partial class AnimatedEmotesSystem : SharedAnimatedEmotesSystem
{
    [Dependency] private AnimationPlayerSystem _anim = default!;
    [Dependency] private IPrototypeManager _prot = default!;

    private const string FlipKey = "emoteAnimFlip";
    private const string SpinKey = "emoteAnimSpin";
    private const string JumpKey = "emoteAnimJump";
    private const string DanceKey = "emoteAnimDance";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimatedEmotesComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationFlipEmoteEvent>(OnFlip);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationSpinEmoteEvent>(OnSpin);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationJumpEmoteEvent>(OnJump);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationDanceEmoteEvent>(OnDance);
    }

    private void PlayEmote(EntityUid uid, Animation anim, string animationKey)
    {
        if (_anim.HasRunningAnimation(uid, animationKey))
            return;

        _anim.Play(uid, anim, animationKey);
    }

    private void OnHandleState(EntityUid uid, AnimatedEmotesComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not AnimatedEmotesComponentState state)
            return;

        if (state.Sequence == component.LastSequence)
            return;

        component.LastSequence = state.Sequence;
        component.Emote = state.Emote;

        if (state.Emote == null || !_prot.TryIndex(state.Emote.Value, out var emote))
            return;

        if (emote.Event != null)
            RaiseLocalEvent(uid, emote.Event);
    }

    private void OnFlip(Entity<AnimatedEmotesComponent> ent, ref AnimationFlipEmoteEvent args)
    {
        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(500),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(360), 0.25f),
                    }
                }
            }
        };

        PlayEmote(ent, anim, FlipKey);
    }

    private void OnSpin(Entity<AnimatedEmotesComponent> ent, ref AnimationSpinEmoteEvent args)
    {
        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(675),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(TransformComponent),
                    Property = nameof(TransformComponent.LocalRotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(0), 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.075f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.075f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.075f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.075f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.075f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.075f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.075f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.075f),
                    }
                }
            }
        };

        PlayEmote(ent, anim, SpinKey);
    }

    private void OnJump(Entity<AnimatedEmotesComponent> ent, ref AnimationJumpEmoteEvent args)
    {
        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(250),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0, 0.35f), 0.125f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.125f),
                    }
                }
            }
        };

        PlayEmote(ent, anim, JumpKey);
    }

    private void OnDance(Entity<AnimatedEmotesComponent> ent, ref AnimationDanceEmoteEvent args)
    {
        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(2400),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(-0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(-0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(-0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(-0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(-0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(-0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(-0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.07f, -0.05f), 0.15f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.15f),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(-10), 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(10), 0.3f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(-10), 0.3f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(10), 0.3f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(-10), 0.3f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(10), 0.3f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(-10), 0.3f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(10), 0.3f),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0.3f),
                    }
                }
            }
        };

        PlayEmote(ent, anim, DanceKey);
    }
}
