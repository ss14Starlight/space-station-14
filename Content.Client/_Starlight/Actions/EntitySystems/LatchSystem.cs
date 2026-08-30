using System.Numerics;
using Content.Client._Starlight.Actions.Components;
using Content.Client._Starlight.Actions.Overlays;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Actions.Events;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Animations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Actions.EntitySystems;

/// <summary>
/// Client-side visual effects for latching: K9 head-shake on Bite Harder, a
/// teeth vignette and latcher outline shown to the target while latched.
/// </summary>
public sealed partial class LatchSystem : SharedLatchSystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string BiteShakeAnimationKey = "latch-bite-shake";
    private const float ShakeLength = 0.25f;
    private const int ShakeCount = 4;
    private const float ShakeMagnitude = 0.08f;

    private const string OutlineShaderId = "latch-target-outline";
    private static readonly ProtoId<ShaderPrototype> OutlineShaderProto = "LatchTargetOutline";

    private readonly LatchVignetteOverlay _vignette = new();
    private ShaderInstance _outline = default!;

    public override void Initialize()
    {
        base.Initialize();

        _outline = _prototypes.Index(OutlineShaderProto).Instance();

        SubscribeNetworkEvent<LatchBiteShakeEvent>(OnBiteShake);
        SubscribeLocalEvent<LatchBiteShakeVisualsComponent, AnimationCompletedEvent>(OnShakeAnimationCompleted);

        SubscribeLocalEvent<LatchedComponent, AfterAutoHandleStateEvent>(OnLatchedStartup);
        SubscribeLocalEvent<LatchedComponent, ComponentShutdown>(OnLatchedShutdown);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
    }

    private void OnLatchedStartup(Entity<LatchedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        _vignette.BiteIntensity = 0f;
        _overlayMan.AddOverlay(_vignette);
        Highlight(ent.Comp.Latcher);
    }

    private void OnLatchedShutdown(Entity<LatchedComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        _overlayMan.RemoveOverlay(_vignette);
        Unhighlight(ent.Comp.Latcher);
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        if (!TryComp<LatchedComponent>(ev.Entity, out var latched))
            return;

        _vignette.BiteIntensity = 0f;
        _overlayMan.AddOverlay(_vignette);
        Highlight(latched.Latcher);
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        _overlayMan.RemoveOverlay(_vignette);

        if (TryComp<LatchedComponent>(ev.Entity, out var latched))
            Unhighlight(latched.Latcher);
    }

    /// <summary>
    /// Outlines the latcher's sprite - visible only on this client, so the
    /// target can pick the K9 out even if it's obscured or hard to spot.
    /// </summary>
    private void Highlight(EntityUid latcher)
    {
        if (TryComp<SpriteComponent>(latcher, out var sprite))
            _sprite.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(OutlineShaderId, _outline));
    }

    private void Unhighlight(EntityUid latcher)
    {
        if (TryComp<SpriteComponent>(latcher, out var sprite))
            _sprite.RemovePostShader(sprite, OutlineShaderId);
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

        // If I'm the one being bitten, spike the vignette's teeth in.
        if (_player.LocalEntity is { } local &&
            TryComp<LatchedComponent>(local, out var latched) &&
            latched.Latcher == entity)
        {
            _vignette.BiteIntensity = 1f;
        }
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
