using Content.Server.Atmos.EntitySystems;
using Content.Shared.EntityEffects;
using Content.Shared._Starlight.EntityEffects.Effects.Atmos;

namespace Content.Server._Starlight.EntityEffects.Effects.Atmos;

/// <summary>
/// This effect adjusts the gas temperature this entity is currently on.
/// The amount changed is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class AddHeatEntityEffectSystem : EntityEffectSystem<TransformComponent, AddHeat>
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<AddHeat> args)
    {
        var tileMix = _atmosphere.GetContainingMixture(entity.AsNullable(), false, true);
        if (tileMix == null) return;
        _atmosphere.AddHeat(tileMix, args.Effect.Heat * args.Scale);
    }
}
