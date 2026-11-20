using System.Numerics;
using Content.Shared._Starlight.Silicons.Bots;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Containers;

namespace Content.Client._Starlight.Silicons.Bots;

public sealed partial class MuleSystem : SharedMuleSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private const string LoadLayer = "mule-load-";
    private const string HoverKey = "mule-hover";

    /// <summary>
    /// How far up the load sits on the platform.
    /// </summary>
    private static readonly Vector2 LoadOffset = new(0f, 10f / 32f);

    /// <summary>
    /// The sprites are drawn hovering; switched off it sits this far lower, on the floor.
    /// </summary>
    private static readonly Vector2 Landed = new(0f, -4f / 32f);

    /// <summary>
    /// How high it bobs while hovering.
    /// </summary>
    private static readonly Vector2 Bob = new(0f, 1f / 32f);

    private static readonly TimeSpan LiftTime = TimeSpan.FromSeconds(0.4);
    private static readonly TimeSpan BobTime = TimeSpan.FromSeconds(1);

    [SubscribeLocalEvent]
    private void OnStartup(Entity<MuleComponent> ent, ref ComponentStartup args)
    {
        _sprite.SetOffset(ent.Owner, ent.Comp.On ? Vector2.Zero : Landed);
        ent.Comp.ShownHovering = ent.Comp.On;
        if (ent.Comp.On)
            Play(ent, Vector2.Zero, Bob, BobTime, true);
    }

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<MuleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.ShownHovering == ent.Comp.On)
            return;

        // Lift off or settle down from wherever it is now.
        ent.Comp.ShownHovering = ent.Comp.On;
        var from = CompOrNull<SpriteComponent>(ent)?.Offset ?? Vector2.Zero;
        Play(ent, from, ent.Comp.On ? Vector2.Zero : Landed, LiftTime, false);
    }

    [SubscribeLocalEvent]
    private void OnAnimationCompleted(Entity<MuleComponent> ent, ref AnimationCompletedEvent args)
    {
        // Keep bobbing for as long as it's on.
        if (args.Key == HoverKey && args.Finished && ent.Comp.On)
            Play(ent, Vector2.Zero, Bob, BobTime, true);
    }

    private void Play(EntityUid uid, Vector2 from, Vector2 to, TimeSpan time, bool andBack)
    {
        var track = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Offset),
            InterpolationMode = AnimationInterpolationMode.Linear,
            KeyFrames =
            {
                new AnimationTrackProperty.KeyFrame(from, 0f),
                new AnimationTrackProperty.KeyFrame(to, (float) time.TotalSeconds),
            },
        };
        if (andBack)
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(from, (float) time.TotalSeconds));

        _animation.Stop(uid, HoverKey);
        _animation.Play(uid, new Animation
        {
            Length = andBack ? time * 2 : time,
            AnimationTracks = { track },
        }, HoverKey);
    }

    [SubscribeLocalEvent]
    private void OnInserted(Entity<MuleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            UpdateLoad(ent);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<MuleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            UpdateLoad(ent);
    }

    /// <summary>
    /// Draws a copy of the load's sprite on top of the bot.
    /// </summary>
    private void UpdateLoad(Entity<MuleComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        for (var i = 0; _sprite.LayerExists((ent, sprite), LoadLayer + i); i++)
        {
            _sprite.RemoveLayer((ent, sprite), LoadLayer + i);
        }

        // Riders draw themselves; only what's on the platform is copied.
        if (!Container.TryGetContainer(ent, ent.Comp.ContainerId, out var container)
            || container.ContainedEntities.Count == 0
            || !TryComp<SpriteComponent>(container.ContainedEntities[0], out var loadSprite))
            return;

        var n = 0;
        foreach (var layer in loadSprite.AllLayers)
        {
            if (!layer.Visible || layer is not SpriteComponent.Layer { ActualRsi: { } rsi } source)
                continue;

            var index = _sprite.AddRsiLayer((ent, sprite), source.State, rsi);
            _sprite.LayerSetColor((ent, sprite), index, source.Color);
            _sprite.LayerSetOffset((ent, sprite), index, source.Offset + LoadOffset);
            _sprite.LayerMapSet((ent, sprite), LoadLayer + n++, index);
        }
    }
}
