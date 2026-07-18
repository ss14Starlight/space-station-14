using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using Robust.Client.Animations;
using Content.Shared._Starlight.Animations;

namespace Content.Client._Starlight.Animations;

public sealed partial class AnimateOnSpawnSystem : EntitySystem
{
    [Dependency] private IGameTiming Timing = default!;
    [Dependency] private SpriteSystem _spriteSystem = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private AnimationPlayerSystem _animationSystem = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimateOnSpawnComponent, ComponentStartup>(OnCompStart);
        SubscribeLocalEvent<AnimateOnSpawnComponent, AnimationCompletedEvent>(OnAnimationComplete);

        _sawmill = _log.GetSawmill("AnimateOnSpawnSystem");
    }

    private void OnAnimationComplete(Entity<AnimateOnSpawnComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != "spawnAnimation")
            return;
        if (!HasComp<AnimationPlayerComponent>(ent) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _spriteSystem.LayerSetVisible((ent.Owner, sprite), AnimateOnSpawnVisualLayers.Animation, false);
        _appearanceSystem.SetData(ent, AnimateOnSpawnVisualState.Animating, false);
    }

    private void OnCompStart(Entity<AnimateOnSpawnComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<AnimationPlayerComponent>(ent) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, (SpriteComponent?)sprite);
        if (!_spriteSystem.LayerMapTryGet(spriteEnt, AnimateOnSpawnVisualLayers.Animation, out var layer, false))
            return;

        var rsi = _spriteSystem.LayerGetEffectiveRsi(spriteEnt, layer);
        if (rsi is null || !rsi.TryGetState(ent.Comp.AnimationState, out var state))
            return;
        var animLength = state.AnimationLength;

        var anim = new Animation
        {
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = AnimateOnSpawnVisualLayers.Animation,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(state.StateId, 0f)
                    },
                },
            },
            Length = TimeSpan.FromSeconds(animLength),
        };

        _spriteSystem.LayerSetVisible(spriteEnt, AnimateOnSpawnVisualLayers.Animation, true);
        _animationSystem.Play(ent, anim, "spawnAnimation");

        _appearanceSystem.SetData(ent, AnimateOnSpawnVisualState.Animating, true);
    }
}
