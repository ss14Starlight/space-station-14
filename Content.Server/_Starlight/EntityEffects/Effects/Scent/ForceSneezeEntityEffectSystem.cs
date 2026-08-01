using Content.Shared._Starlight.EntityEffects.Effects.Scent;
using Content.Shared._Starlight.Scent;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared._Starlight.Scent.Systems;
using Content.Shared.EntityEffects;

namespace Content.Server._Starlight.EntityEffects.Effects.Scent;

/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class ForceSneezeEntityEffectSystem : EntityEffectSystem<SmellerComponent, ForceSneeze>
{
    [Dependency] private readonly SharedScentSystem _scent = default!;

    protected override void Effect(Entity<SmellerComponent> entity, ref EntityEffectEvent<ForceSneeze> args)
    {
        _scent.ForceSneeze(entity, args.Effect.Lockout);
    }
}
