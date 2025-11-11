using Content.Shared.EntityEffects;
using Content.Shared.Starlight.EntityEffects.Components;
using Content.Shared.Starlight.EntityEffects.EntitySystems;
using Content.Shared.Starlight.EntityEffects.Effects;

namespace Content.Server.EntityEffects.Effects.Dissolve;

public sealed partial class FlammableEntityEffectSystem : EntityEffectSystem<FlammableComponent, Flammable>
{
    [Dependency] private readonly SharedDissolvableSystem _dissolvable = default!;

    protected override void Effect(Entity<FlammableComponent> entity, ref EntityEffectEvent<Flammable> args)
    {
        // The multiplier is determined by if the entity is already on fire, and if the multiplier for existing FireStacks has a value.
        // If both of these are true, we use the MultiplierOnExisting value, otherwise we use the standard Multiplier.
        var multiplier = entity.Comp.FireStacks == 0f || args.Effect.MultiplierOnExisting == null ? args.Effect.Multiplier : args.Effect.MultiplierOnExisting.Value;

        _flammable.AdjustFireStacks(entity, args.Scale * multiplier, entity.Comp);
    }
}