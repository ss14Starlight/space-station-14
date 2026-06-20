using Content.Server.Weather;
using Content.Shared._Starlight.Weather.Effects;
using Content.Shared.EntityEffects;

namespace Content.Server._Starlight.Weather.Effects;

public sealed partial class WeatherEntityEffectSystem : EntitySystem
{
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeatherEntityEffectComponent, WeatherEntityAffectedEvent>(OnEntityAffected);
    }

    private void OnEntityAffected(Entity<WeatherEntityEffectComponent> ent, ref WeatherEntityAffectedEvent args)
    {
        if (ent.Comp.EffectPrototype is { } protoId)
            _effects.TryApplyEffect(args.Target, protoId, ent.Comp.Scale);
        else if (ent.Comp.Effects is { Length: > 0 })
            _effects.ApplyEffects(args.Target, ent.Comp.Effects, ent.Comp.Scale, ent.Owner);
    }
}
