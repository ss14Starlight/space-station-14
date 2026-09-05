using Content.Shared.Mobs.Components;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

public sealed partial class CauseZombieInfectionEntityEffectsSystem
{
    private void RomerolBites(Entity<MobStateComponent> entity)
    {
        var infection = EnsureComp<BloodStreamInfectionComponent>(entity);
        //this one is for romerol, gives five bites on the same trigger threshold
        infection.InfectiousBiteCount = 5;
    }

}

public sealed partial class CureZombieInfectionEntityEffectsSystem
{
    private void RemoveBloodStreamInfection(Entity<MobStateComponent> entity) =>
        RemComp<BloodStreamInfectionComponent>(entity);
}

public sealed partial class DelayZombieInfectionUptickEntityEffectsSystem : EntityEffectSystem<MobStateComponent, DelayZombieInfectionUptick>
{
    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<DelayZombieInfectionUptick> args)
    {
        if (!TryComp<BloodStreamInfectionComponent>(entity.Owner, out var infection))
            return;


    }
}

public sealed partial class ResumeZombieInfectionUptickEntityEffectsSystem : EntityEffectSystem<MobStateComponent, ResumeZombieInfectionUptick>
{
    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<ResumeZombieInfectionUptick> args)
    {
        if (!TryComp<BloodStreamInfectionComponent>(entity.Owner, out var infection))
            return;


    }
}

public sealed partial class ResumeZombieInfectionUptick : EntityEffectBase<ResumeZombieInfectionUptick>
{
}

public sealed partial class DelayZombieInfectionUptick : EntityEffectBase<DelayZombieInfectionUptick>
{
}
