using Content.Server._Starlight.Achievement;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.EntityEffects;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
/// Polymorphs this entity into another entity.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class PolymorphEntityEffectSystem : EntityEffectSystem<PolymorphableComponent, Shared.EntityEffects.Effects.Polymorph>
{
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private AchievementSystem _achievement = default!;

    protected override void Effect(Entity<PolymorphableComponent> entity, ref EntityEffectEvent<Shared.EntityEffects.Effects.Polymorph> args)
    {
        _polymorph.PolymorphEntity(entity, args.Effect.Prototype);
        var effect = args.Effect;
        if (effect.Prototype.Equals("ArtifactLizard"))
        {
            _achievement.QueueUnlockAchievement(entity, "weh");
        }
    }
}
