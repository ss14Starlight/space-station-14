using Content.Shared.Coordinates;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Xenobiology;

/// <summary>
/// Handles the general behavior of slimes.
/// </summary>
public sealed class SlimeSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public List<SlimeSplitRecord> SlimeSplitRecords = new();

    public sealed class SlimeSplitRecord(Entity<SlimeComponent?> slime, int splitAmount)
    {
        public Entity<SlimeComponent?> Slime = slime;
        public int SplitAmount = splitAmount;
    }

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var record in SlimeSplitRecords)
        {
            TrySplitSlime(record.Slime, record.SplitAmount);
        }
        SlimeSplitRecords.Clear();
    }

    /// <summary>
    /// Attempts to eat a target.
    /// </summary>
    /// <param name="slime">The slime entity.</param>
    /// <param name="target">The target entity ID.</param>
    /// <returns>Returns false if the slime was unable to eat the target. Returns true otherwise.</returns>
    public bool TryEat(Entity<SlimeComponent?> slime, EntityUid target)
    {
        if (!Resolve(slime, ref slime.Comp, false)) return false;

        if (!_interaction.InRangeUnobstructed(slime.Owner, target, range: 0.75f)) return false;
        if (!TryComp<DamageableComponent>(target, out var damage)) return false;

        if (!_damageableSystem.TryChangeDamage(target, slime.Comp.DamageOnEat, out var returnDamage, ignoreResistances: true)) return false;
        _audioSystem.PlayPredicted(new SoundPathSpecifier("/Audio/Effects/bite.ogg"), slime.Owner, null, AudioParams.Default.WithVariation(0.05F));

        var vector = (Transform(target).LocalPosition - Transform(slime.Owner).LocalPosition).Normalized();
        RaiseNetworkEvent(new SlimeBiteAnimationMessage()
        {
            Entity = GetNetEntity(slime.Owner, MetaData(slime.Owner)),
            Angle = Angle.FromWorldVec(vector),
        }, Filter.Pvs(slime.Owner, 0.5F));

        if (returnDamage.AnyPositive())
        {
            _hungerSystem.ModifyHunger(slime, slime.Comp.NutritionOnHit.Float());
        }

        return true;
    }

    public bool TrySplitSlime(Entity<SlimeComponent?> slime, int split_amount)
    {
        if (!Resolve(slime, ref slime.Comp, false)) return false;
        var newNutrition = 0F;
        if (TryComp<HungerComponent>(slime, out var hunger))
            newNutrition = _hungerSystem.GetHunger(hunger) / split_amount;
        var random = _robustRandom.GetRandom();
        for (int i = 0; i < split_amount; i++)
        {
            string protoName;
            if (slime.Comp.MutationChance >= 1.0f && slime.Comp.MutationOnMaxSplit != null)
            {
                protoName = slime.Comp.MutationOnMaxSplit;
            }
            else if (random.NextFloat() < slime.Comp.MutationChance && slime.Comp.SplitIntoMutation.Count > 0)
            {
                var randomIndex = random.Next(slime.Comp.SplitIntoMutation.Count);
                protoName = slime.Comp.SplitIntoMutation[randomIndex];
            }
            else
            {
                protoName = slime.Comp.SplitInto;
            }
            var split = _entityManager.PredictedSpawnAtPosition(protoName, slime.Owner.ToCoordinates());
            SlimeComponent? comp = null;
            if (Resolve(split, ref comp))
            {
                _hungerSystem.SetHunger(split, newNutrition);
                comp.MutationChance = slime.Comp.MutationChance;
            }
            else return false;
        }
        _entityManager.PredictedQueueDeleteEntity(slime.Owner);
        return true;
    }

    public bool QueueSlimeSplit(Entity<SlimeComponent?> slime, int splitAmount)
    {
        SlimeSplitRecords.Add(new(slime, splitAmount));
        return true;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeBiteAnimationMessage : EntityEventArgs
{
    public NetEntity Entity;
    public Angle Angle;
}
