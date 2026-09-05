using Content.Shared._Starlight.Light;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Light.EntitySystems;

public sealed class SLPointLightSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<PointLightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PointLightComponent, ComponentRemove>(OnRemove);
    }

    private void OnMapInit(Entity<PointLightComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<SLPointLightComponent>(ent);
    }

    private void OnRemove(Entity<PointLightComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent))
            RemCompDeferred<SLPointLightComponent>(ent);
    }
}
