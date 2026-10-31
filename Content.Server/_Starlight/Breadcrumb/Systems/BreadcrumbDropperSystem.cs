using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared._Starlight.Breadcrumb.Components;
using Content.Shared._Starlight.Breadcrumb.Systems;

namespace Content.Server._Starlight.Breadcrumb.Systems;

public sealed partial class BreadcrumbDropperSystem : SharedBreadcrumbDropperSystem
{
    [Dependency] private ExpendableLightSystem _expendableLight = null!;

    [SubscribeLocalEvent]
    public void OnBreadcrumbMapInit(Entity<BreadcrumbComponent> crumb, ref MapInitEvent ev)
    {
        if (TryComp<ExpendableLightComponent>(crumb, out var expendableLight))
            _expendableLight.TryActivate((crumb, expendableLight));
    }

}
