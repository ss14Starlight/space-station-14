using Content.Client.Light.EntitySystems;
using Content.Shared._Starlight.Breadcrumb.Components;

namespace Content.Client._Starlight.Breadcrumb.Systems;

public sealed partial class BreadcrumbDroppingSystem : EntitySystem
{
    [Dependency] private LightBehaviorSystem _lightBehavior = null!;
    [Dependency] private ExpendableLightSystem _expendableLight = null!;

    [SubscribeLocalEvent]
    public void OnStartup(Entity<BreadcrumbDroppingComponent> dropping, ref ComponentStartup ev)
    {
        //_lightBehavior.SetOffset(dropping.Owner, dropping.Comp.PhaseOffset)

    }
}
