using Content.Shared.Power.Components;
using Content.Shared.UserInterface;
// ReSharper disable CheckNamespace
namespace Content.Server.Power.EntitySystems;

public sealed partial class ActivatableUIRequiresPowerSystem
{
    private partial bool HasPowerCellFallback(EntityUid uid, ActivatableUIRequiresPowerComponent component) =>
        component.AllowPowerCellFallback && HasComp<ActivatableUIRequiresPowerCellComponent>(uid);
}
