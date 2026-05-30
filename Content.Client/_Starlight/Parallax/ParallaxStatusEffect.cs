using Content.Client.Parallax.Managers;
using Content.Shared._Starlight.Weather;

namespace Content.Client._Starlight.Parallax;

public sealed class ParallaxStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly IParallaxManager _parallax = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ParallaxStatusEffectComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ParallaxStatusEffectComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(Entity<ParallaxStatusEffectComponent> ent, ref ComponentInit args)
    {
        if (!_parallax.IsLoaded(ent.Comp.Parallax))
            _parallax.LoadParallaxByName(ent.Comp.Parallax);
    }

    private void OnComponentShutdown(Entity<ParallaxStatusEffectComponent> ent, ref ComponentShutdown args)
    {
        if (!_parallax.IsLoaded(ent.Comp.Parallax))
            return;

        bool currentlyused = false;
        var query = EntityQueryEnumerator<ParallaxStatusEffectComponent>();
        while (query.MoveNext(out var uid, out var parallax))
            if (uid != ent.Owner && parallax.Parallax == ent.Comp.Parallax)
                currentlyused = true;

        if (!currentlyused)
            _parallax.UnloadParallax(ent.Comp.Parallax);
    }
}
