using JetBrains.Annotations;
using Content.Shared.EntityEffects;
using Content.Shared.Database;
using Content.Shared.Tag;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Content.Shared.Starlight.EntityEffects.Components;
using Content.Shared.Starlight.EntityEffects.EntitySystems;

namespace Content.Shared.Starlight.EntityEffects.Effects;

/// <summary>
/// Makes this entity sentient. Allows ghost to take it over if it's not already occupied.
/// Optionally also allows this entity to speak.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class DissolvableReactionEntityEffectSystem : EntityEffectSystem<DissolvableComponent, DissolvableReaction>
{
    [Dependency] private readonly SharedDissolvableSystem _dissolvable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly EntityManager _entMan = default!;

    protected override void Effect(Entity<DissolvableComponent> entity, ref EntityEffectEvent<DissolvableReaction> args)
    {
        if (_tag.HasTag(entity, "UnDissolvable")) // Yeah, this is hardcode but.... Idk
            return;

        entity.Comp.Damage = args.Effect.Damage;

        // Sets the multiplier for FireStacks to MultiplierOnExisting is 0 or greater and target already has FireStacks
        var multiplier = entity.Comp.DissolveStacks != 0f && args.Effect.MultiplierOnExisting >= 0 ? args.Effect.MultiplierOnExisting : args.Effect.Multiplier;

        _dissolvable.AdjustDissolveStacks(entity, args.Scale * multiplier, entity);

        var coordinates = _entMan.GetComponent<TransformComponent>(entity).Coordinates;
        if (_entityLookup.GetEntitiesInRange<ThermiteComponent>(coordinates, 1f).Count == 0)
            PredictedSpawnAtPosition(args.Effect.DissolveEffectPrototype, coordinates);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DissolvableReaction : EntityEffectBase<DissolvableReaction>
{
    [DataField]
    public float Multiplier = 0.05f;

    [DataField]
    public float MultiplierOnExisting = -1f;
    
    [DataField(required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = new();

    [DataField]
    public EntProtoId? DissolveEffectPrototype = "ThermiteEntity";

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-dissolvable-reaction",
                ("chance", Probability));
    }

    public override LogImpact? Impact => LogImpact.Medium;
}
