using System.Numerics;
using Content.Client.Animations;
using Content.Client.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    private const string FadeAnimationKey = "melee-fade";
    private const string SlashAnimationKey = "melee-slash";
    private const string ThrustAnimationKey = "melee-thrust";

    /// <summary>
    /// Does all of the melee effects for a player that are predicted, i.e. character lunge and weapon animation.
    /// </summary>
    public override void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        var lunge = GetLungeAnimation(localPos);

        // Stop any existing lunges on the user.
        _animation.Stop(user, MeleeLungeKey);
        _animation.Play(user, lunge, MeleeLungeKey);

        if (localPos == Vector2.Zero || animation == null)
            return;

        // Starlight Begin - Mech wideswing
        var originEntity = GetOriginEntity(user);

        if (!_xformQuery.TryGetComponent(originEntity, out var userXform) || userXform.MapID == MapId.Nullspace)
            return;
        // Starlight End

        var animationUid = Spawn(animation, userXform.Coordinates);

        if (!TryComp<SpriteComponent>(animationUid, out var sprite)
            || !TryComp<WeaponArcVisualsComponent>(animationUid, out var arcComponent))
        {
            return;
        }

        var length = 1f;
        var offset = 1f;

        var spriteRotation = Angle.Zero;
        // Starlight-start
        if (TryComp(weapon, out MeleeWeaponComponent? meleeWeaponComponent))
        {
            length = (1 / meleeWeaponComponent.AttackRate) * 0.6f;
            offset = meleeWeaponComponent.AnimationOffset;

            if (arcComponent.Animation != WeaponArcAnimation.None)
            {
                if (user != weapon
                    && TryComp(weapon, out SpriteComponent? weaponSpriteComponent))
                    _sprite.CopySprite((weapon, weaponSpriteComponent), (animationUid, sprite));

                spriteRotation = meleeWeaponComponent.WideAnimationRotation;

                if (meleeWeaponComponent.SwingLeft)
                    angle *= -1;
            }
        }
        // Starlight-end
        _sprite.SetRotation((animationUid, sprite), localPos.ToWorldAngle());

        var xform = _xformQuery.GetComponent(animationUid);
        TrackUserComponent track;

        switch (arcComponent.Animation)
        {
            case WeaponArcAnimation.Slash:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetSlashAnimation((animationUid, sprite), angle, spriteRotation, length, offset), SlashAnimationKey);
                // Starlight-start
                _animation.Play(animationUid, GetSlashFadeAnimation(sprite,
                    fadeInEnd: length * 0.15f,
                    fadeOutStart: arcComponent.Fadeout ? length * 0.5f : length + 0.15f,
                    fadeOutEnd: length + 0.15f), FadeAnimationKey);
                // Starlight-end
                break;
            case WeaponArcAnimation.Thrust:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetThrustAnimation((animationUid, sprite), offset, spriteRotation, length), ThrustAnimationKey);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, length * 0.5f, length + 0.15f), FadeAnimationKey);
                break;
            //Starlight begin
            case WeaponArcAnimation.OldSlash:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetOldSlashAnimation(sprite, angle, spriteRotation), SlashAnimationKey);
                if(arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, length * 0.5f, length + 0.15f), FadeAnimationKey);
                break;
            case WeaponArcAnimation.OldThrust:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetOldThrustAnimation((animationUid, sprite), (float)Math.Clamp(localPos.Length()/2f,0.2,1f), spriteRotation), ThrustAnimationKey);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, 0.05f, 0.15f), FadeAnimationKey);
                break;
            //Starlight end
            case WeaponArcAnimation.None:
                var (mapPos, mapRot) = TransformSystem.GetWorldPositionRotation(userXform);
                var worldPos = mapPos + (mapRot - userXform.LocalRotation).RotateVec(localPos);
                var newLocalPos = Vector2.Transform(worldPos, TransformSystem.GetInvWorldMatrix(xform.ParentUid));

                // Starlight Begin - Smooth slide animation for entities without WeaponArcVisualsComponent
                var startLocalPos = Vector2.Transform(mapPos, TransformSystem.GetInvWorldMatrix(xform.ParentUid));
                TransformSystem.SetLocalPositionNoLerp(animationUid, startLocalPos, xform);

                var slideLength = length > 0 ? length : 0.15f;

                var slideAnim = new Animation
                {
                    Length = TimeSpan.FromSeconds(slideLength / 1.5f),
                    AnimationTracks =
                    {
                        new AnimationTrackComponentProperty
                        {
                            ComponentType = typeof(TransformComponent),
                            Property = nameof(TransformComponent.LocalPosition),
                            InterpolationMode = AnimationInterpolationMode.Linear,
                            KeyFrames =
                            {
                                new AnimationTrackProperty.KeyFrame(startLocalPos, 0f),
                                new AnimationTrackProperty.KeyFrame(newLocalPos, slideLength / 1.5f),
                            }
                        }
                    }
                };
                _animation.Play(animationUid, slideAnim, "none-slide");
                _animation.Play(animationUid, GetFadeAnimation(sprite, 0f, slideLength / 1.5f, startAlpha: 0f, endAlpha: 1f), FadeAnimationKey);
                // Starlight End
                break;
        }
    }

    private Animation GetSlashAnimation(Entity<SpriteComponent> sprite, Angle arc, Angle spriteRotation, float length, float offset)
    {
        var startRotation = sprite.Comp.Rotation + (arc * 0.5f);
        var endRotation = sprite.Comp.Rotation - (arc * 0.5f);

        var startRotationOffset = startRotation.RotateVec(new Vector2(0f, -offset * 0.9f));
        var minRotationOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, -offset * 1.1f));
        var endRotationOffset = endRotation.RotateVec(new Vector2(0f, -offset * 0.9f));

        startRotation += spriteRotation;
        endRotation += spriteRotation;
        sprite.Comp.NoRotation = true;

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length + 0.05f),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,0.0f), length * 0.0f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,0.5f), length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,1.0f), length * 0.15f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,0.9f), length * 0.20f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation,endRotation,0.80f), length * 0.6f, Easings.OutQuart)
                    },
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset,endRotationOffset,0.0f), length * 0.0f),
                        new AnimationTrackProperty.KeyFrame(minRotationOffset, length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset,endRotationOffset,1.0f), length * 0.15f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset,endRotationOffset,0.80f), length * 0.6f, Easings.OutQuart)
                    }
                },
            }
        };
    }


    #region Starlight

    private Animation GetSlashFadeAnimation(SpriteComponent sprite, float fadeInEnd, float fadeOutStart, float fadeOutEnd)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(fadeOutEnd),
            AnimationTracks =
        {
            new AnimationTrackComponentProperty()
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Color),
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(0f), 0f),
                    new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(1f), fadeInEnd),
                    new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(1f), fadeOutStart),
                    new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(0f), fadeOutEnd),
                }
            }
        }
        };
    }

    #endregion

    private Animation GetThrustAnimation(Entity<SpriteComponent> sprite, float offset, Angle spriteRotation, float length)
    {
        var startOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, 0f));
        var endOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, -offset * 1.2f));

        _sprite.SetRotation(sprite.AsNullable(), sprite.Comp.Rotation + spriteRotation);

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0f), length * 0f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.65f), length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 1f), length * 0.20f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.9f), length * 0.30f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.7f), length * 0.60f, Easings.OutQuart)
                    }
                },
            }
        };
    }

    private Animation GetFadeAnimation(SpriteComponent sprite, float start, float end, float startAlpha = 1f, float endAlpha = 0f) // Starlight-edit
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(end),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(startAlpha), start), // Starlight-edit
                        new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(endAlpha), end) // Starlight-edit
                    }
                }
            }
        };
    }

    /// <summary>
    /// Get the sprite offset animation to use for mob lunges.
    /// </summary>
    private Animation GetLungeAnimation(Vector2 direction)
    {
        const float length = 0.35f;
        var dir = direction.Normalized();

        // Starlight-start

        // Timings
        const float anticipationEnd = 0.08f;
        const float actionPeak = 0.14f;
        const float recoveryMid = 0.22f; // innertion
        const float recoveryEnd = length;

        // Starlight-end

        return new Animation
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        // Starlight-start
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(-dir * 0.08f, anticipationEnd),
                        new AnimationTrackProperty.KeyFrame(dir * 0.22f, actionPeak),
                        new AnimationTrackProperty.KeyFrame(-dir * 0.04f, recoveryMid),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, recoveryEnd),
                        // Starlight-end
                    },
                },
            },
        };
    }

    //Starlight begin
    private Animation GetOldSlashAnimation(SpriteComponent sprite, Angle arc, Angle spriteRotation)
    {
        const float slashStart = 0.03f;
        const float slashEnd = 0.065f;
        const float length = slashEnd + 0.05f;
        var startRotation = sprite.Rotation + arc / 2;
        var endRotation = sprite.Rotation - arc / 2;
        var startRotationOffset = startRotation.RotateVec(new Vector2(0f, -1f));
        var endRotationOffset = endRotation.RotateVec(new Vector2(0f, -1f));
        startRotation += spriteRotation;
        endRotation += spriteRotation;

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startRotation, 0f),
                        new AnimationTrackProperty.KeyFrame(startRotation, slashStart),
                        new AnimationTrackProperty.KeyFrame(endRotation, slashEnd)
                    }
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startRotationOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(startRotationOffset, slashStart),
                        new AnimationTrackProperty.KeyFrame(endRotationOffset, slashEnd)
                    }
                },
            }
        };
    }

    private Animation GetOldThrustAnimation(Entity<SpriteComponent> sprite, float distance, Angle spriteRotation)
    {
        const float thrustEnd = 0.05f;
        const float length = 0.15f;
        var startOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, -distance / 5f));
        var endOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, -distance));
        _sprite.SetRotation(sprite.AsNullable(), sprite.Comp.Rotation + spriteRotation);

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(endOffset, thrustEnd),
                        new AnimationTrackProperty.KeyFrame(endOffset, length),
                    }
                },
            }
        };
    }
    //Starlight end

    /// <summary>
    /// Updates the effect positions to follow the user
    /// </summary>
    private void UpdateEffects()
    {
        var query = EntityQueryEnumerator<TrackUserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var arcComponent, out var xform))
        {
            if (arcComponent.User == null || EntityManager.Deleted(arcComponent.User))
                continue;

            Vector2 targetPos = TransformSystem.GetWorldPosition(arcComponent.User.Value);

            if (arcComponent.Offset != Vector2.Zero)
            {
                var entRotation = TransformSystem.GetWorldRotation(xform);
                targetPos += entRotation.RotateVec(arcComponent.Offset);
            }

            TransformSystem.SetWorldPosition(uid, targetPos);
        }
    }
}
