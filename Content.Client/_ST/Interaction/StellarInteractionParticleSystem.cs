// SPDX-FileCopyrightText: 2026 Janet Blackquill <uhhadd@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client._Moffstation.Interaction;
using Content.Shared._ST.Interaction;
using Content.Shared._Starlight.CCVar;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._ST.Interaction;

public sealed partial class StellarInteractionParticleSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private IConfigurationManager _cfg = default!; // Starlight
    [Dependency] private IPlayerManager _player = default!; // Starlight

    private InteractionParticleMode _interactionParticleMode; // Starlight
    private const string AnimateKey = "particle-animation";

    private static readonly Dictionary<StellarInteractionParticleType, EntProtoId> InteractionParticleIds = new ()
    {
        { StellarInteractionParticleType.Use, "StellarInteractionParticleUse" },
        { StellarInteractionParticleType.Pull, "StellarInteractionParticlePull" },
        { StellarInteractionParticleType.InHand, "StellarInteractionParticleUse" },
    };

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.InteractionParticlesMode, value => _interactionParticleMode = (InteractionParticleMode) value, true); // Starlight, interaction particle config

        SubscribeAllEvent<StellarInteractionParticleEvent>(OnInteractionParticle);
    }

    private void OnInteractionParticle(StellarInteractionParticleEvent ev)
    {
        if (_interactionParticleMode == InteractionParticleMode.None) // Starlight, check if interaction particles are enabled
            return; // Starlight, if not, don't display them!

        var performer = GetEntity(ev.Performer);
        var used = GetEntity(ev.Used);
        var target = GetEntity(ev.Target);


        if (!Exists(performer) || !Exists(target))
            return;

        var actor = performer; // Starlight
        var type = ev.Type;
        if (type == StellarInteractionParticleType.Pull)
        {
            (performer, target) = (target, performer);
        }

        var performerXform = Transform(performer);
        var targetXform = Transform(target);
        if (performerXform.MapID == MapId.Nullspace || targetXform.MapID == MapId.Nullspace)
            return;

        // if the interaction is happening across parent boundaries (ie inhand or in a bag or something)
        // override it with an inhand particle effect
        if (performerXform.ParentUid != targetXform.ParentUid)
        {
            if (type == StellarInteractionParticleType.Pull)
                return;

            type = StellarInteractionParticleType.InHand;
        }

        if (!ShouldShowParticle(type, actor)) // Starlight, check if the particle should be shown for the actor
            return; // Starlight, if the particle shouldn't be shown, don't display it

        // Moffstation - start - Add in cooldown - Starlight, moved it after the visibility check, no point in tracking particles that won't be seen
        if (TryComp<InteractionParticleTrackerComponent>(performer, out var tracker))
        {
            if (_timing.CurTime < tracker.ExpireTime)
                return;

            RemComp<InteractionParticleTrackerComponent>(performer);
        }
        // Moffstation - End

        var performerTargetDelta = targetXform.LocalPosition - performerXform.LocalPosition;
        var inHandDelta = new Vector2(0, 0.75f);
        var particle = Spawn(InteractionParticleIds[type], performerXform.Coordinates);

        if (type == StellarInteractionParticleType.InHand)
        {
            used = target;
            _xform.SetParent(particle, performer);
        }

        if (used is { } usedEntity && Exists(usedEntity) && TryComp<SpriteComponent>(usedEntity, out var usedSprite))
        {
            _sprite.CopySprite((usedEntity, usedSprite), particle);
            _sprite.SetDrawDepth(particle, (int) Shared.DrawDepth.DrawDepth.Effects);
        }

        var sprite = Comp<SpriteComponent>(particle);
        sprite.NoRotation = true;
        var spriteColor = sprite.Color;
        var animation = type switch
        {
            StellarInteractionParticleType.Use => GetUseAnimation(performerTargetDelta, spriteColor),
            StellarInteractionParticleType.Pull => GetPullAnimation(performerTargetDelta, spriteColor),
            StellarInteractionParticleType.InHand => GetUseAnimation(inHandDelta, spriteColor),
            _ => throw new ArgumentOutOfRangeException(nameof(ev), $"Interaction particle event has unknown particle type {type}"),
        };
        _animation.Play(particle, animation, AnimateKey);

        EnsureComp<InteractionParticleTrackerComponent>(performer).ExpireTime = _timing.CurTime + ev.Cooldown; // Starlight
    }

    private Animation GetUseAnimation(Vector2 endOffset, Color color)
    {
        var startRotation = _random.NextAngle(Angle.FromDegrees(-40), Angle.FromDegrees(40));
        var endRotation = Angle.Zero;
        var startScale = new Vector2(0.3f, 0.3f);
        var endScale = new Vector2(1f, 1f);
        var rotationLength = TimeSpan.FromMilliseconds(600);

        var startOffset = new Vector2();
        var offsetLength = TimeSpan.FromMilliseconds(250);

        var startColor = color.WithAlpha(color.A * 0.9f);
        var endColor = color.WithAlpha(0f);
        var colorLength = rotationLength + offsetLength;

        return new Animation()
        {
            Length = colorLength,

            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startRotation, 0f),
                        new AnimationTrackProperty.KeyFrame(endRotation, (float)rotationLength.TotalSeconds, Easings.OutBack),
                    },
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startScale, 0f),
                        new AnimationTrackProperty.KeyFrame(endScale, (float)rotationLength.TotalSeconds, Easings.OutBack),
                    },
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(endOffset, (float)offsetLength.TotalSeconds, Easings.OutBack),
                    },
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startColor, 0f),
                        new AnimationTrackProperty.KeyFrame(startColor, (float)rotationLength.TotalSeconds),
                        new AnimationTrackProperty.KeyFrame(endColor, (float)offsetLength.TotalSeconds, Easings.InOutCirc),
                    },
                },
            },
        };
    }

    private Animation GetPullAnimation(Vector2 endOffset, Color color)
    {
        var rotationLength = TimeSpan.FromMilliseconds(8f * (1000f / 12f));

        var startOffset = new Vector2();
        var offsetLength = TimeSpan.FromMilliseconds(4f * (1000f / 12f));

        var endColor = color.WithAlpha(0f);

        return new Animation
        {
            Length = rotationLength,

            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(endOffset, (float)rotationLength.TotalSeconds, Easings.InOutCirc),
                    },
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(color, 0f),
                        new AnimationTrackProperty.KeyFrame(color, (float)offsetLength.TotalSeconds),
                        new AnimationTrackProperty.KeyFrame(endColor, (float)rotationLength.TotalSeconds, Easings.InOutCirc),
                    },
                },
            },
        };
    }

    #region Starlight
    /// <summary>
    /// Determines whether a particle of the specified type should be shown for the given actor.
    /// </summary>
    /// <param name="type">The type of the interaction particle.</param>
    /// <param name="actor">The entity for which to determine particle visibility.</param>
    /// <returns>True if the particle should be shown, false otherwise.</returns>
    private bool ShouldShowParticle(StellarInteractionParticleType type, EntityUid actor)
    {
        var isLocalActor =
            _player.LocalEntity is { } localPlayer &&
            localPlayer == actor;

        return _interactionParticleMode switch
        {
            InteractionParticleMode.All =>
                type != StellarInteractionParticleType.InHand || isLocalActor,

            InteractionParticleMode.WithoutInHand =>
                type != StellarInteractionParticleType.InHand,

            InteractionParticleMode.None => false,

            // Invalid manually entered CVar values fail closed.
            _ => false,
        };
    }
    #endregion
}
