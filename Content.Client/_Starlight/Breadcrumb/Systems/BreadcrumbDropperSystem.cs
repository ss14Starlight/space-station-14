using Content.Shared._Starlight.Breadcrumb.Systems;

namespace Content.Client._Starlight.Breadcrumb.Systems;

/// <summary>
/// Client-side concrete so the shared <see cref="SharedBreadcrumbDropperSystem"/> ticks here too
/// (prediction). Crumb coloring is handled by <see cref="BreadcrumbVisualizer"/>, not here.
/// </summary>
public sealed partial class BreadcrumbDropperSystem : SharedBreadcrumbDropperSystem
{
}
